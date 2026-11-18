using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Sales;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Visits
{
    /// <summary>Site visit requests and the visit calendar: confirm with a time and agent, decline, move, record the outcome.</summary>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AgentService _agents;
        private readonly SiteVisitService _visits;
        private readonly SalesReports _reports;

        public IndexModel(ApplicationDbContext db, AgentService agents, SiteVisitService visits, SalesReports reports)
        {
            _db = db;
            _agents = agents;
            _visits = visits;
            _reports = reports;
        }

        /// <summary>"requests" (default: waiting for an answer), "upcoming", "past" or "all".</summary>
        [BindProperty(SupportsGet = true)] public string? Show { get; set; }
        [BindProperty] public DateTime? Start { get; set; }
        [BindProperty] public string? AgentUserId { get; set; }
        [BindProperty] public string? Note { get; set; }

        public string Current => string.IsNullOrEmpty(Show) ? "requests" : Show;
        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public List<SiteVisit> Items { get; private set; } = new();
        public Dictionary<string, int> Counts { get; private set; } = new();
        public Dictionary<string, string> Names { get; private set; } = new();
        /// <summary>Staff per company (for the agent choice).</summary>
        public Dictionary<Guid, List<(string Id, string Name)>> Staff { get; private set; } = new();
        public bool CanManage => Access.Has(Perm.VisitsManage);

        public async Task OnGetAsync()
        {
            Access = await HttpContext.AccessAsync();
            var scope = await _agents.ScopeAsync(Access);
            var all = _db.SiteVisits.AsNoTracking().VisibleTo(scope);
            var now = DateTime.Now;
            Counts = new()
            {
                ["requests"] = await all.CountAsync(v => v.Status == SiteVisitStatuses.Requested || v.Status == SiteVisitStatuses.RescheduleRequested),
                ["upcoming"] = await all.CountAsync(v => v.Status == SiteVisitStatuses.Confirmed),
            };
            var q = Current switch
            {
                "upcoming" => all.Where(v => v.Status == SiteVisitStatuses.Confirmed).OrderBy(v => v.ScheduledStart),
                "past" => all.Where(v => !SiteVisitStatuses.Open.Contains(v.Status)).OrderByDescending(v => v.ScheduledStart ?? v.PreferredDate),
                "all" => all.OrderByDescending(v => v.CreatedOn),
                _ => all.Where(v => v.Status == SiteVisitStatuses.Requested || v.Status == SiteVisitStatuses.RescheduleRequested).OrderBy(v => v.PreferredDate),
            };
            Items = await q.Include(v => v.Customer).Include(v => v.Project).Include(v => v.Company).Include(v => v.ApartmentHouse).Include(v => v.History)
                .Take(300).AsSplitQuery().ToListAsync();
            Names = await _reports.NamesAsync(Items.Where(v => v.AgentUserId != null).Select(v => v.AgentUserId!));
            if (CanManage)
            {
                var companies = Items.Select(v => v.CompanyId).Distinct().ToList();
                var staff = await _db.AppUsers.AsNoTracking().Where(u => u.AccountType == AccountType.CompanyStaff && u.HousingCompanyId != null && companies.Contains(u.HousingCompanyId.Value))
                    .Select(u => new { u.Id, Company = u.HousingCompanyId!.Value }).ToListAsync();
                var names = await _reports.NamesAsync(staff.Select(s => s.Id));
                Staff = staff.GroupBy(s => s.Company).ToDictionary(g => g.Key, g => g.Select(s => (s.Id, names.GetValueOrDefault(s.Id, s.Id))).OrderBy(s => s.Item2).ToList());
            }
        }

        public async Task<IActionResult> OnPostConfirmAsync(Guid id)
        {
            var (access, scope) = await ScopeAsync();
            var result = Start is DateTime start
                ? await _visits.ConfirmAsync(access, scope, id, start, AgentUserId, Note, User.Identity?.Name)
                : AccessResult.Fail("Choose the start time.");
            return Done(result);
        }

        public async Task<IActionResult> OnPostRejectAsync(Guid id)
        {
            var (access, scope) = await ScopeAsync();
            return Done(await _visits.RejectAsync(access, scope, id, Note, User.Identity?.Name));
        }

        public Task<IActionResult> OnPostCompletedAsync(Guid id) => Close(id, true);
        public Task<IActionResult> OnPostNoShowAsync(Guid id) => Close(id, false);

        private async Task<IActionResult> Close(Guid id, bool attended)
        {
            var (access, scope) = await ScopeAsync();
            return Done(await _visits.CloseAsync(access, scope, id, attended, Note, User.Identity?.Name));
        }

        private async Task<(UserAccess, SalesScope)> ScopeAsync()
        {
            var access = await HttpContext.AccessAsync();
            return (access, await _agents.ScopeAsync(access));
        }

        private IActionResult Done(AccessResult result)
        {
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { show = Show });
        }

        public int Count(string key) => Counts.TryGetValue(key, out var n) ? n : 0;
    }
}
