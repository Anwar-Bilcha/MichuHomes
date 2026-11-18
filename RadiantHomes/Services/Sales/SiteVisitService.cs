using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Sales;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.CustomerPortal;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Services.Sales
{
    public sealed record VisitRequest(Guid? ProjectId, Guid? ApartmentId, DateTime Date, string Time, string? Notes);

    /// <summary>
    /// Site visits: a customer asks to see a project (or one home in it) on a day and time slot; the company confirms it
    /// with a time and an agent (an agent can't have two visits within an hour), declines it with a reason, or moves it;
    /// the customer can cancel or ask for another time; afterwards the agent records that it happened (with the outcome)
    /// or that the customer didn't come. Every change is kept in the visit's history, the other side is notified and the
    /// customer's lead moves along (interested → visit scheduled → visit done).
    /// </summary>
    public sealed class SiteVisitService
    {
        public const int MaxDaysAhead = 60;
        public static readonly TimeSpan AgentGap = TimeSpan.FromHours(1);

        private readonly ApplicationDbContext _db;
        private readonly AgentService _agents;
        private readonly LeadService _leads;
        private readonly CustomerNotifications _customers;

        public SiteVisitService(ApplicationDbContext db, AgentService agents, LeadService leads, CustomerNotifications customers)
        {
            _db = db;
            _agents = agents;
            _leads = leads;
            _customers = customers;
        }

        // ---------- Customer ----------

        public async Task<AccessResult> RequestAsync(string customerUserId, VisitRequest input)
        {
            var account = await _db.AppUsers.Include(u => u.Customer).FirstOrDefaultAsync(u => u.Id == customerUserId);
            if (account?.Customer is not { } customer || account.AccountType != AccountType.Customer)
                return AccessResult.Fail("Complete your customer profile before asking for a site visit.");

            Guid projectId; Guid? apartmentId = null; string about;
            if (input.ApartmentId is Guid aid)
            {
                var home = await _db.ApartmentHouses.AsNoTracking().Where(a => a.Id == aid)
                    .Select(a => new { a.Title, ProjectId = a.Building!.ProjectId, CompanyStatus = a.Building.Project!.Company!.Status }).FirstOrDefaultAsync();
                if (home is null || home.CompanyStatus != CompanyStatuses.Approved) return AccessResult.Fail("This home can't be visited through Michu Homes.");
                projectId = home.ProjectId; apartmentId = aid; about = home.Title;
            }
            else if (input.ProjectId is Guid pid)
            {
                var project = await _db.Projects.AsNoTracking().Where(p => p.ProjectId == pid)
                    .Select(p => new { p.ProjectName, CompanyStatus = p.Company!.Status }).FirstOrDefaultAsync();
                if (project is null || project.CompanyStatus != CompanyStatuses.Approved) return AccessResult.Fail("This project can't be visited through Michu Homes.");
                projectId = pid; about = project.ProjectName;
            }
            else return AccessResult.Fail("Choose the project or home you want to visit.");

            var error = CheckSlot(input.Date, input.Time) ?? CheckNotes(input.Notes);
            if (error is not null) return AccessResult.Fail(error);
            if (await _db.SiteVisits.AnyAsync(v => v.CustomerUserId == customerUserId && v.ProjectId == projectId && SiteVisitStatuses.Open.Contains(v.Status)))
                return AccessResult.Fail("You already have an open visit request for this project. Change or cancel it under My visits.");

            var companyId = await _db.Projects.Where(p => p.ProjectId == projectId).Select(p => p.HousingCompanyId).FirstAsync();
            var visit = new SiteVisit
            {
                Id = Guid.NewGuid(), Reference = AgentService.NewReference("V"), CompanyId = companyId, ProjectId = projectId, ApartmentHouseId = apartmentId,
                CustomerId = customer.Id, CustomerUserId = customerUserId, PreferredDate = input.Date.Date, PreferredTime = input.Time,
                Notes = Clean(input.Notes), Status = SiteVisitStatuses.Requested, CreatedOn = DateTime.UtcNow,
                AgentUserId = await _agents.PickAgentAsync(projectId),
            };
            _db.SiteVisits.Add(visit);
            History(visit, null, SiteVisitStatuses.Requested, $"Asked for {Slot(visit)}.", NameOf(customer));
            var lead = await _leads.TouchAsync(companyId, projectId, apartmentId, customer, "SiteVisit", "Visit", $"Asked to visit {about} on {Slot(visit)} ({visit.Reference}).", LeadStages.Interested);
            visit.LeadId = lead.Id;
            NotifyCompany(visit, "Site visit requested", $"{NameOf(customer)} asked to visit {about} on {Slot(visit)} ({visit.Reference}).");
            await _db.SaveChangesAsync();
            return AccessResult.Ok($"Visit request {visit.Reference} sent. The company will confirm the time.", visit.Id.ToString());
        }

        public async Task<AccessResult> CancelByCustomerAsync(string customerUserId, Guid visitId, string? reason)
        {
            var visit = await Load(visitId);
            if (visit is null || visit.CustomerUserId != customerUserId) return AccessResult.Fail("That visit doesn't exist.");
            if (!SiteVisitStatuses.Open.Contains(visit.Status)) return AccessResult.Fail("This visit can no longer be cancelled.");
            if (CheckNotes(reason) is { } error) return AccessResult.Fail(error);
            Move(visit, SiteVisitStatuses.Cancelled, Clean(reason) ?? "Cancelled by the customer.", NameOf(visit.Customer));
            NotifyCompany(visit, "Site visit cancelled", $"{NameOf(visit.Customer)} cancelled visit {visit.Reference} ({Slot(visit)}).");
            await _db.SaveChangesAsync();
            return AccessResult.Ok("Your visit was cancelled.");
        }

        public async Task<AccessResult> RequestNewTimeAsync(string customerUserId, Guid visitId, DateTime date, string time, string? note)
        {
            var visit = await Load(visitId);
            if (visit is null || visit.CustomerUserId != customerUserId) return AccessResult.Fail("That visit doesn't exist.");
            if (!SiteVisitStatuses.Open.Contains(visit.Status)) return AccessResult.Fail("This visit can no longer be changed.");
            var error = CheckSlot(date, time) ?? CheckNotes(note);
            if (error is not null) return AccessResult.Fail(error);
            visit.PreferredDate = date.Date;
            visit.PreferredTime = time;
            visit.ScheduledStart = null;
            Move(visit, SiteVisitStatuses.RescheduleRequested, $"Asked for {Slot(visit)}." + (Clean(note) is { } n ? $" {n}" : ""), NameOf(visit.Customer));
            NotifyCompany(visit, "New visit time requested", $"{NameOf(visit.Customer)} asked to move visit {visit.Reference} to {Slot(visit)}.");
            await _db.SaveChangesAsync();
            return AccessResult.Ok("Your new time was sent. The company will confirm it.");
        }

        // ---------- Staff ----------

        /// <summary>Confirm (or move) a visit: a start time and the agent who meets the customer.</summary>
        public async Task<AccessResult> ConfirmAsync(UserAccess actor, SalesScope scope, Guid visitId, DateTime start, string? agentUserId, string? note, string? actorName)
        {
            var visit = await Load(visitId);
            if (visit is null || !actor.Has(Perm.VisitsManage) || !scope.CanSee(visit.CompanyId, visit.ProjectId, visit.AgentUserId)) return AccessResult.Fail("You can't manage this visit.");
            if (!SiteVisitStatuses.Open.Contains(visit.Status)) return AccessResult.Fail("This visit is closed.");
            if (start < DateTime.Now) return AccessResult.Fail("The visit must start in the future.");
            if (start > DateTime.Today.AddDays(MaxDaysAhead + 1)) return AccessResult.Fail($"Visits can be booked up to {MaxDaysAhead} days ahead.");
            if (CheckNotes(note) is { } noteError) return AccessResult.Fail(noteError);
            agentUserId = string.IsNullOrWhiteSpace(agentUserId) ? (visit.AgentUserId ?? actor.UserId) : agentUserId;
            var agent = await _db.AppUsers.Include(u => u.Customer).FirstOrDefaultAsync(u => u.Id == agentUserId);
            if (agent is null || agent.AccountType != AccountType.CompanyStaff || agent.HousingCompanyId != visit.CompanyId) return AccessResult.Fail("Choose one of the company's staff as the agent.");

            var from = start - AgentGap;
            var to = start + AgentGap;
            var clash = await _db.SiteVisits.AsNoTracking().Where(v => v.Id != visit.Id && v.AgentUserId == agent.Id && v.Status == SiteVisitStatuses.Confirmed
                && v.ScheduledStart > from && v.ScheduledStart < to).Select(v => new { v.Reference, v.ScheduledStart }).FirstOrDefaultAsync();
            if (clash is not null)
                return AccessResult.Fail($"{AccessManagementService.DisplayName(agent)} already has visit {clash.Reference} at {clash.ScheduledStart:HH:mm} that day. Choose another time or agent.");

            var moved = visit.Status == SiteVisitStatuses.Confirmed;
            visit.ScheduledStart = start;
            visit.AgentUserId = agent.Id;
            Move(visit, SiteVisitStatuses.Confirmed, $"{(moved ? "Moved to" : "Confirmed for")} {start:ddd d MMM HH:mm} with {AccessManagementService.DisplayName(agent)}." + (Clean(note) is { } n ? $" {n}" : ""), actorName);
            var lead = await _leads.TouchAsync(visit.CompanyId, visit.ProjectId, visit.ApartmentHouseId, visit.Customer, "SiteVisit", "Visit", $"Visit {visit.Reference} {(moved ? "moved to" : "confirmed for")} {start:d MMM HH:mm}.", LeadStages.VisitScheduled);
            visit.LeadId ??= lead.Id;
            _customers.Add(visit.CustomerId, moved ? "Site visit moved" : "Site visit confirmed",
                $"{visit.Company.Name}: your visit to {visit.Project.ProjectName} is on {start:dddd d MMMM} at {start:HH:mm}. {AccessManagementService.DisplayName(agent)} will meet you." + (Clean(note) is { } m ? $" {m}" : ""),
                EntityAffecteds.UserProfiles, visit.Id.ToString(), "/Portal/Visits");
            await _db.SaveChangesAsync();
            return AccessResult.Ok(moved ? "Visit moved; the customer was told." : "Visit confirmed; the customer was told.");
        }

        public async Task<AccessResult> RejectAsync(UserAccess actor, SalesScope scope, Guid visitId, string? reason, string? actorName)
        {
            var visit = await Load(visitId);
            if (visit is null || !actor.Has(Perm.VisitsManage) || !scope.CanSee(visit.CompanyId, visit.ProjectId, visit.AgentUserId)) return AccessResult.Fail("You can't manage this visit.");
            if (!SiteVisitStatuses.Open.Contains(visit.Status)) return AccessResult.Fail("This visit is closed.");
            reason = Clean(reason);
            if (reason is null) return AccessResult.Fail("Tell the customer why.");
            if (CheckNotes(reason) is { } error) return AccessResult.Fail(error);
            Move(visit, SiteVisitStatuses.Rejected, reason, actorName);
            _customers.Add(visit.CustomerId, "Site visit declined", $"{visit.Company.Name} can't host your visit {visit.Reference}: {reason}", EntityAffecteds.UserProfiles, visit.Id.ToString(), "/Portal/Visits");
            await _db.SaveChangesAsync();
            return AccessResult.Ok("Visit declined; the customer was told.");
        }

        /// <summary>After the visit: it happened (with the outcome) or the customer didn't come.</summary>
        public async Task<AccessResult> CloseAsync(UserAccess actor, SalesScope scope, Guid visitId, bool attended, string? outcome, string? actorName)
        {
            var visit = await Load(visitId);
            if (visit is null || !actor.Has(Perm.VisitsManage) || !scope.CanSee(visit.CompanyId, visit.ProjectId, visit.AgentUserId)) return AccessResult.Fail("You can't manage this visit.");
            if (visit.Status != SiteVisitStatuses.Confirmed || visit.ScheduledStart is not DateTime start) return AccessResult.Fail("Only confirmed visits can be closed.");
            if (start > DateTime.Now.AddMinutes(30)) return AccessResult.Fail("The visit hasn't happened yet.");
            outcome = Clean(outcome);
            if (outcome is { Length: > 1000 }) return AccessResult.Fail("Keep the outcome under 1000 characters.");
            visit.Outcome = outcome;
            Move(visit, attended ? SiteVisitStatuses.Completed : SiteVisitStatuses.NoShow, outcome ?? (attended ? "The customer visited." : "The customer didn't come."), actorName);
            await _leads.TouchAsync(visit.CompanyId, visit.ProjectId, visit.ApartmentHouseId, visit.Customer, "SiteVisit", "Visit",
                attended ? $"Visited ({visit.Reference}){(outcome is null ? "." : $": {outcome}")}" : $"Didn't come to visit {visit.Reference}.",
                attended ? LeadStages.VisitCompleted : null);
            if (attended)
                _customers.Add(visit.CustomerId, "Thank you for visiting", $"Thank you for visiting {visit.Project.ProjectName}. Ask {visit.Company.Name} anything in Messages.", EntityAffecteds.UserProfiles, visit.Id.ToString(), "/Portal/Visits");
            await _db.SaveChangesAsync();
            return AccessResult.Ok(attended ? "Visit recorded." : "Recorded as not attended.");
        }

        // ---------- Helpers ----------

        public static string Slot(SiteVisit v) => v.ScheduledStart is DateTime s ? $"{s:ddd d MMM HH:mm}" : $"{v.PreferredDate:ddd d MMM} {v.PreferredTime}";

        public static string? CheckSlot(DateTime date, string? time)
        {
            if (time is null || !VisitSlots.All.Contains(time)) return "Choose one of the offered times.";
            if (date.Date <= DateTime.Today) return "Choose a day from tomorrow on.";
            if (date.Date > DateTime.Today.AddDays(MaxDaysAhead)) return $"Choose a day within the next {MaxDaysAhead} days.";
            return null;
        }

        private static string? CheckNotes(string? notes) => notes is { Length: > 500 } ? "Keep notes under 500 characters." : null;

        private Task<SiteVisit?> Load(Guid id) =>
            _db.SiteVisits.Include(v => v.Customer).Include(v => v.Company).Include(v => v.Project).FirstOrDefaultAsync(v => v.Id == id);

        private void Move(SiteVisit visit, string to, string? note, string? by)
        {
            History(visit, visit.Status, to, note, by);
            visit.Status = to;
        }

        private void History(SiteVisit visit, string? from, string to, string? note, string? by) =>
            _db.SiteVisitStatusChanges.Add(new SiteVisitStatusChange
            {
                SiteVisitId = visit.Id, SiteVisit = visit, FromStatus = from, ToStatus = to,
                Note = note is { Length: > 1000 } ? note[..1000] : note, ChangedByName = by, ChangedOn = DateTime.UtcNow,
            });

        private void NotifyCompany(SiteVisit visit, string title, string message) =>
            _db.Notifications.Add(new Notification
            {
                Title = title, Message = message, ReceiverCompanyId = visit.CompanyId, RecipientRole = MichuHomesRoles.MichuAdmin,
                Datatype = "SiteVisits", DataKey = visit.Id.ToString(), RedirectUrl = "/Staff/Visits", CreatedAt = DateTime.Now,
            });

        private static string NameOf(Customer c) => $"{c.FirstName} {c.LastName}".Trim();
        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
