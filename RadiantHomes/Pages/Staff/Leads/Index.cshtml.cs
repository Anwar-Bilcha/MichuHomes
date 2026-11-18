using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Sales;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Leads
{
    /// <summary>The sales pipeline: leads by stage, filtered by agent, temperature, source and text; follow-ups due.</summary>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AgentService _agents;
        private readonly SalesReports _reports;

        public IndexModel(ApplicationDbContext db, AgentService agents, SalesReports reports)
        {
            _db = db;
            _agents = agents;
            _reports = reports;
        }

        /// <summary>A stage, "open" (default) or "all".</summary>
        [BindProperty(SupportsGet = true)] public string? Stage { get; set; }
        [BindProperty(SupportsGet = true)] public string? Agent { get; set; }
        [BindProperty(SupportsGet = true)] public string? Temperature { get; set; }
        [BindProperty(SupportsGet = true)] public string? Source { get; set; }
        [BindProperty(SupportsGet = true)] public string? Q { get; set; }
        [BindProperty(SupportsGet = true)] public bool Due { get; set; }

        public string CurrentStage => string.IsNullOrEmpty(Stage) ? "open" : Stage;
        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public Dictionary<string, int> StageCounts { get; private set; } = new();
        public List<Lead> Items { get; private set; } = new();
        public Dictionary<string, string> Names { get; private set; } = new();
        public List<(string Id, string Name)> AgentChoices { get; private set; } = new();
        public int DueCount { get; private set; }

        public async Task OnGetAsync()
        {
            Access = await HttpContext.AccessAsync();
            var scope = await _agents.ScopeAsync(Access);
            var all = _db.Leads.AsNoTracking().VisibleTo(scope);
            StageCounts = await all.GroupBy(l => l.Stage).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N);
            var now = DateTime.Now;
            DueCount = await all.CountAsync(l => l.ClosedOn == null && l.NextFollowUpOn != null && l.NextFollowUpOn <= now);

            var q = all;
            q = CurrentStage switch
            {
                "open" => q.Where(l => l.ClosedOn == null),
                "all" => q,
                _ => q.Where(l => l.Stage == CurrentStage),
            };
            if (Agent == "none") q = q.Where(l => l.AgentUserId == null);
            else if (Agent == "me") { var me = Access.UserId; q = q.Where(l => l.AgentUserId == me); }
            else if (!string.IsNullOrEmpty(Agent)) q = q.Where(l => l.AgentUserId == Agent);
            if (!string.IsNullOrEmpty(Temperature)) q = q.Where(l => l.Temperature == Temperature);
            if (!string.IsNullOrEmpty(Source)) q = q.Where(l => l.Source == Source);
            if (Due) q = q.Where(l => l.ClosedOn == null && l.NextFollowUpOn != null && l.NextFollowUpOn <= now);
            if (!string.IsNullOrWhiteSpace(Q))
            {
                var text = Q.Trim();
                q = q.Where(l => l.Name.Contains(text) || l.Reference.Contains(text) || (l.Phone != null && l.Phone.Contains(text)) || (l.Email != null && l.Email.Contains(text)));
            }
            Items = await q.Include(l => l.Project).Include(l => l.Company)
                .OrderBy(l => l.NextFollowUpOn == null).ThenBy(l => l.NextFollowUpOn).ThenByDescending(l => l.CreatedOn)
                .Take(300).ToListAsync();

            var agentIds = await all.Where(l => l.AgentUserId != null).Select(l => l.AgentUserId!).Distinct().ToListAsync();
            Names = await _reports.NamesAsync(agentIds);
            AgentChoices = agentIds.Select(id => (id, Names.GetValueOrDefault(id, id))).OrderBy(a => a.Item2).ToList();
        }

        public int Count(string stage) => StageCounts.TryGetValue(stage, out var n) ? n : 0;
        public int OpenCount => LeadStages.OpenStages.Sum(Count);
    }
}
