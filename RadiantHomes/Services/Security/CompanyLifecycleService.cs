using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Security;

namespace RadiantHomes.Services.Security
{
    public enum CompanyAction { Approve, Reject, RequestInfo, Suspend, Reactivate, Resubmit }

    /// <summary>
    /// A real estate company's approval lifecycle. Only the platform approves, rejects, asks for information, suspends
    /// and reactivates; the company itself can only resubmit after being asked for information or rejected. Company
    /// administration works only while the company is Approved (see AccessControlService). Every change is kept in
    /// CompanyStatusChanges, audited and announced in the company's (or platform's) notifications.
    /// </summary>
    public sealed class CompanyLifecycleService
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public CompanyLifecycleService(ApplicationDbContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public static (string Permission, string[] From, string To, bool ReasonRequired) Rule(CompanyAction action) => action switch
        {
            CompanyAction.Approve => (Perm.CompaniesApprove, new[] { CompanyStatuses.Pending, CompanyStatuses.InfoRequested, CompanyStatuses.Rejected }, CompanyStatuses.Approved, false),
            CompanyAction.Reject => (Perm.CompaniesApprove, new[] { CompanyStatuses.Pending, CompanyStatuses.InfoRequested }, CompanyStatuses.Rejected, true),
            CompanyAction.RequestInfo => (Perm.CompaniesApprove, new[] { CompanyStatuses.Pending }, CompanyStatuses.InfoRequested, true),
            CompanyAction.Suspend => (Perm.CompaniesSuspend, new[] { CompanyStatuses.Approved }, CompanyStatuses.Suspended, true),
            CompanyAction.Reactivate => (Perm.CompaniesSuspend, new[] { CompanyStatuses.Suspended }, CompanyStatuses.Approved, false),
            CompanyAction.Resubmit => (Perm.CompanyProfileUpdate, new[] { CompanyStatuses.InfoRequested, CompanyStatuses.Rejected }, CompanyStatuses.Pending, false),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        /// <summary>Actions this user may take on this company now.</summary>
        public static IEnumerable<CompanyAction> AvailableActions(UserAccess actor, Guid companyId, string? status) =>
            Enum.GetValues<CompanyAction>().Where(a =>
            {
                var rule = Rule(a);
                if (!rule.From.Contains(status)) return false;
                return a == CompanyAction.Resubmit ? actor.IsCompanyStaff && actor.CompanyId == companyId && actor.Has(rule.Permission) : actor.IsPlatform && actor.Has(rule.Permission);
            });

        public async Task<AccessResult> ChangeAsync(UserAccess actor, Guid companyId, CompanyAction action, string? reason)
        {
            var rule = Rule(action);
            var platformAction = action != CompanyAction.Resubmit;
            if (platformAction ? !(actor.IsPlatform && actor.Has(rule.Permission)) : !(actor.IsCompanyStaff && actor.CompanyId == companyId && actor.Has(rule.Permission)))
            {
                return AccessResult.Fail("You don't have permission to do that.");
            }

            var company = await _db.HousingCompanies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company is null) return AccessResult.Fail("That company doesn't exist.");
            if (!rule.From.Contains(company.Status))
            {
                return AccessResult.Fail($"{company.Name} is \"{CompanyStatuses.Label(company.Status)}\"; that action isn't possible now.");
            }
            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (rule.ReasonRequired && reason is null) return AccessResult.Fail("Please give a reason. The company will see it.");
            if (reason is { Length: > 1000 }) return AccessResult.Fail("Keep the reason under 1000 characters.");

            var from = company.Status;
            company.Status = rule.To;
            _db.CompanyStatusChanges.Add(new CompanyStatusChange
            {
                CompanyId = companyId,
                FromStatus = from,
                ToStatus = rule.To,
                Reason = reason,
                ChangedByUserId = actor.UserId,
                ChangedByName = await ActorNameAsync(actor),
                ChangedOn = DateTime.UtcNow,
            });

            // Approval gives the company its built-in super admin role, so its first administrator can manage the rest.
            if (rule.To == CompanyStatuses.Approved
                && !await _db.AccessRoles.AnyAsync(r => r.CompanyId == companyId && r.IsSystem && r.Name == PermissionCatalog.CompanySuperAdminRoleName))
            {
                AccessControlSeeder.NewCompanySuperAdmin(_db, companyId);
            }

            var (title, message) = action switch
            {
                CompanyAction.Approve => ("Company approved", $"{company.Name} was approved. You can now use company administration."),
                CompanyAction.Reject => ("Company application rejected", $"{company.Name} was not approved. Reason: {reason}"),
                CompanyAction.RequestInfo => ("More information needed", $"Michu Homes needs more information about {company.Name}: {reason}"),
                CompanyAction.Suspend => ("Company suspended", $"{company.Name} was suspended. Reason: {reason}"),
                CompanyAction.Reactivate => ("Company reactivated", $"{company.Name} is active again."),
                _ => ("Company resubmitted for approval", $"{company.Name} sent its details for approval again.{(reason is null ? "" : " Note: " + reason)}"),
            };
            _db.Notifications.Add(new Notification
            {
                Title = title,
                Message = message,
                ReceiverCompanyId = companyId,
                RecipientRole = platformAction ? MichuHomesRoles.RealEstateAdmin : MichuHomesRoles.MichuAdmin,
                Datatype = EntityAffecteds.HousingCompanies.ToString(),
                DataKey = companyId.ToString(),
                RedirectUrl = platformAction ? "/HousingCompanyAdmin/Status" : $"/Admin/Companies/Details?id={companyId}",
                CreatedAt = DateTime.Now,
            });
            _audit.Add(AuditCategories.Companies, "Company" + action, $"{company.Name}: {CompanyStatuses.Label(from)} → {CompanyStatuses.Label(rule.To)}{(reason is null ? "" : $" ({reason})")}.",
                companyId, "HousingCompany", companyId.ToString(), new { From = from, To = rule.To, reason });
            _db.CompanyStatusChangeAllowed = true; // the DbContext otherwise keeps a company's stored status
            try
            {
                await _db.SaveChangesAsync();
            }
            finally
            {
                _db.CompanyStatusChangeAllowed = false;
            }
            AccessControlService.Invalidate();

            return AccessResult.Ok(action switch
            {
                CompanyAction.Approve => $"{company.Name} was approved.",
                CompanyAction.Reject => $"{company.Name} was rejected.",
                CompanyAction.RequestInfo => $"{company.Name} was asked for more information.",
                CompanyAction.Suspend => $"{company.Name} was suspended.",
                CompanyAction.Reactivate => $"{company.Name} was reactivated.",
                _ => "Your company was sent for approval again.",
            });
        }

        public Task<List<CompanyStatusChange>> HistoryAsync(Guid companyId) =>
            _db.CompanyStatusChanges.AsNoTracking().Where(c => c.CompanyId == companyId).OrderByDescending(c => c.ChangedOn).ThenByDescending(c => c.Id).ToListAsync();

        private async Task<string?> ActorNameAsync(UserAccess actor)
        {
            if (actor.UserId is null) return null;
            var u = await _db.AppUsers.AsNoTracking().Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == actor.UserId);
            return u is null ? null : AccessManagementService.DisplayName(u);
        }
    }
}
