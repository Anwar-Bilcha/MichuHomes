using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Sales;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Leads
{
    /// <summary>One lead: details, stage, agent, activity timeline (calls, meetings, notes), follow-up, and what the
    /// customer did (chats, visits, reservations, calls).</summary>
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AgentService _agents;
        private readonly LeadService _leads;
        private readonly SalesReports _reports;

        public DetailsModel(ApplicationDbContext db, AgentService agents, LeadService leads, SalesReports reports)
        {
            _db = db;
            _agents = agents;
            _leads = leads;
            _reports = reports;
        }

        [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
        [BindProperty] public LeadInput Input { get; set; } = new();
        [BindProperty] public string? Stage { get; set; }
        [BindProperty] public string? LostReason { get; set; }
        [BindProperty] public string? AgentUserId { get; set; }
        [BindProperty] public string? ActivityType { get; set; }
        [BindProperty] public string? ActivityBody { get; set; }
        [BindProperty] public DateTime? NextFollowUpOn { get; set; }

        public Lead Lead { get; private set; } = default!;
        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public LeadFormOptions Options { get; private set; } = new();
        public string? AgentName { get; private set; }
        public List<Conversation> Conversations { get; private set; } = new();
        public List<SiteVisit> Visits { get; private set; } = new();
        public List<ApartmentHouseSaleTransaction> Reservations { get; private set; } = new();
        public List<CallLog> Calls { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var (lead, access, _) = await LoadAsync();
            if (lead is null) return NotFound();
            await BuildAsync(lead, access);
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            var (lead, access, scope) = await LoadAsync();
            if (lead is null) return NotFound();
            return Done(await _leads.UpdateAsync(access, scope, Id, Input, User.Identity?.Name));
        }

        public async Task<IActionResult> OnPostStageAsync()
        {
            var (lead, access, scope) = await LoadAsync();
            if (lead is null) return NotFound();
            return Done(await _leads.ChangeStageAsync(access, scope, Id, Stage ?? string.Empty, LostReason, User.Identity?.Name));
        }

        public async Task<IActionResult> OnPostAssignAsync()
        {
            var (lead, access, scope) = await LoadAsync();
            if (lead is null) return NotFound();
            return Done(await _leads.AssignAsync(access, scope, Id, string.IsNullOrWhiteSpace(AgentUserId) ? null : AgentUserId, User.Identity?.Name));
        }

        public async Task<IActionResult> OnPostActivityAsync()
        {
            var (lead, access, scope) = await LoadAsync();
            if (lead is null) return NotFound();
            return Done(await _leads.AddActivityAsync(access, scope, Id, ActivityType ?? string.Empty, ActivityBody, NextFollowUpOn, User.Identity?.Name));
        }

        private IActionResult Done(AccessResult result)
        {
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { id = Id });
        }

        private async Task<(Lead?, UserAccess, SalesScope)> LoadAsync()
        {
            var access = await HttpContext.AccessAsync();
            var scope = await _agents.ScopeAsync(access);
            var lead = await _db.Leads.AsNoTracking().VisibleTo(scope).Include(l => l.Company).Include(l => l.Project).Include(l => l.ApartmentHouse).Include(l => l.Activities)
                .AsSplitQuery().FirstOrDefaultAsync(l => l.Id == Id);
            return (lead, access, scope);
        }

        private async Task BuildAsync(Lead lead, UserAccess access)
        {
            Lead = lead;
            Access = access;
            Options = await LeadFormOptions.LoadAsync(_db, _reports, access, lead.CompanyId);
            Input = new LeadInput
            {
                CompanyId = lead.CompanyId, Name = lead.Name, Phone = lead.Phone, Email = lead.Email, ProjectId = lead.ProjectId, ApartmentHouseId = lead.ApartmentHouseId,
                Bedrooms = lead.Bedrooms, Budget = lead.Budget, Currency = lead.Currency, Location = lead.Location, Source = lead.Source, Campaign = lead.Campaign,
                Temperature = lead.Temperature, AgentUserId = lead.AgentUserId, NextFollowUpOn = lead.NextFollowUpOn,
            };
            if (lead.AgentUserId is string agent) AgentName = (await _reports.NamesAsync(new[] { agent })).GetValueOrDefault(agent, "Former staff");
            if (lead.CustomerId is Guid customerId)
            {
                Conversations = await _db.Conversations.AsNoTracking().Where(c => c.CustomerId == customerId && c.CompanyId == lead.CompanyId).OrderByDescending(c => c.LastMessageOn).Take(20).ToListAsync();
                Visits = await _db.SiteVisits.AsNoTracking().Include(v => v.Project).Where(v => v.CustomerId == customerId && v.CompanyId == lead.CompanyId).OrderByDescending(v => v.CreatedOn).Take(20).ToListAsync();
                Reservations = await _db.ApartmentHouseSaleTransactions.AsNoTracking().Include(t => t.ApartmentHouse)
                    .Where(t => t.CustomerId == customerId && t.ApartmentHouse.Building!.Project!.HousingCompanyId == lead.CompanyId).OrderByDescending(t => t.TransactionDate).Take(20).ToListAsync();
                var userIds = await _db.AppUsers.AsNoTracking().Where(u => u.CustomerId == customerId).Select(u => u.Id).ToListAsync();
                Calls = await _db.CallLogs.AsNoTracking().Where(c => userIds.Contains(c.CustomerUserId) && c.CompanyId == lead.CompanyId).OrderByDescending(c => c.RequestedOn).Take(20).ToListAsync();
            }
        }
    }
}
