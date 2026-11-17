using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Email;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Users
{
    /// <summary>One staff member: details, roles, effective permissions, activation, password link and history.</summary>
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AccessManagementService _manage;
        private readonly AccessControlService _accessControl;
        private readonly AccountEmails _emails;
        private readonly AuditService _audit;

        public EditModel(ApplicationDbContext db, AccessManagementService manage, AccessControlService accessControl, AccountEmails emails, AuditService audit)
        {
            _db = db;
            _manage = manage;
            _accessControl = accessControl;
            _emails = emails;
            _audit = audit;
        }

        [BindProperty(SupportsGet = true)] public string Id { get; set; } = string.Empty;
        [BindProperty] public DetailsInput Details { get; set; } = new();
        [BindProperty] public List<Guid> RoleIds { get; set; } = new();

        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public AppUser Person { get; private set; } = null!;
        public string Name { get; private set; } = string.Empty;
        public bool Active { get; private set; }
        public bool IsYou => Person.Id == Access.UserId;
        public string? CompanyName { get; private set; }
        public List<CreateModel.RoleOption> Roles { get; private set; } = new();
        public HashSet<Guid> CurrentRoleIds { get; private set; } = new();
        public List<IGrouping<string, PermissionDefinition>> Effective { get; private set; } = new();
        public bool EffectiveLimitedByApproval { get; private set; }
        public List<AuditLog> History { get; private set; } = new();
        public bool CanEmail => _emails.CanSend;

        public class DetailsInput
        {
            [Required, StringLength(150), Display(Name = "Full name")]
            public string FullName { get; set; } = string.Empty;
            [Phone, Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await LoadAsync()) return NotFound();
            Details = new DetailsInput { FullName = Name, PhoneNumber = Person.PhoneNumber };
            return Page();
        }

        public async Task<IActionResult> OnPostDetailsAsync()
        {
            if (!await LoadAsync()) return NotFound();
            if (!ModelState.IsValid) return Page();
            return Done(await _manage.UpdateStaffUserAsync(Access, Id, Details.FullName, Details.PhoneNumber));
        }

        public async Task<IActionResult> OnPostRolesAsync()
        {
            if (!await LoadAsync()) return NotFound();
            return Done(await _manage.SetUserRolesAsync(Access, Id, RoleIds));
        }

        public async Task<IActionResult> OnPostDeactivateAsync()
        {
            if (!await LoadAsync()) return NotFound();
            return Done(await _manage.SetUserActiveAsync(Access, Id, active: false));
        }

        public async Task<IActionResult> OnPostReactivateAsync()
        {
            if (!await LoadAsync()) return NotFound();
            return Done(await _manage.SetUserActiveAsync(Access, Id, active: true));
        }

        public async Task<IActionResult> OnPostPasswordLinkAsync()
        {
            if (!await LoadAsync()) return NotFound();
            if (!Access.Has(Perm.UsersResetPassword)) return Done(AccessResult.Fail("You don't have permission to send password links."));
            if (!CanEmail) return Done(AccessResult.Fail("Emails aren't set up on this server, so no link can be sent."));
            if (!Active) return Done(AccessResult.Fail("Reactivate the account first."));
            try
            {
                await _emails.SendPasswordLinkAsync(Person, invitation: !Person.EmailConfirmed && Person.PasswordHash is null);
            }
            catch
            {
                return Done(AccessResult.Fail("The email could not be sent. Check the email settings and try again."));
            }
            _audit.Add(AuditCategories.Users, "PasswordLinkSent", $"Password link sent to {Name} ({Person.Email}).", Person.HousingCompanyId, nameof(AppUser), Person.Id);
            await _db.SaveChangesAsync();
            return Done(AccessResult.Ok($"A link to choose a new password was emailed to {Person.Email}."));
        }

        private IActionResult Done(AccessResult result)
        {
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { id = Id });
        }

        private async Task<bool> LoadAsync()
        {
            Access = await HttpContext.AccessAsync();
            var person = await _manage.VisibleStaff(Access).Include(u => u.Company).Include(u => u.Customer).FirstOrDefaultAsync(u => u.Id == Id);
            if (person is null) return false;
            Person = person;
            Name = AccessManagementService.DisplayName(person);
            Active = AccessManagementService.IsActive(person);
            CompanyName = person.AccountType == AccountType.PlatformStaff ? "Michu Homes (platform staff)" : person.Company?.Name;

            var roles = await _manage.AssignableRoles(person).AsNoTracking().Include(r => r.Permissions).OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name).ToListAsync();
            var oversight = Access.IsPlatform && person.AccountType == AccountType.CompanyStaff;
            Roles = roles.Select(r => new CreateModel.RoleOption(r.Id, r.Name, r.Description, r.IsSystem, r.Permissions.Count,
                oversight || r.Permissions.All(p => Access.Has(p.PermissionKey)), r.IsActive)).ToList();
            CurrentRoleIds = (await _db.UserAccessRoles.Where(ur => ur.UserId == person.Id).Select(ur => ur.RoleId).ToListAsync()).ToHashSet();

            var effective = await _accessControl.LoadAsync(person.Id);
            EffectiveLimitedByApproval = effective.IsCompanyStaff && !effective.CompanyApproved;
            Effective = PermissionCatalog.All.Where(p => effective.Has(p.Key)).GroupBy(p => p.Module).ToList();

            History = await _db.AuditLogs.AsNoTracking().Where(a => a.EntityType == nameof(AppUser) && a.EntityId == person.Id)
                .OrderByDescending(a => a.OccurredOn).Take(15).ToListAsync();
            return true;
        }
    }
}
