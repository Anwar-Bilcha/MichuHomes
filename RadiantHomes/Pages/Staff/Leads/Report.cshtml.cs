using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Leads
{
    /// <summary>Sales and CRM figures for a period, within the viewer's sales scope (agents: their own projects).</summary>
    public class ReportModel : PageModel
    {
        private readonly AgentService _agents;
        private readonly SalesReports _reports;

        public ReportModel(AgentService agents, SalesReports reports)
        {
            _agents = agents;
            _reports = reports;
        }

        [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }

        public LeadReport Report { get; private set; } = new();
        public UserAccess Access { get; private set; } = UserAccess.Anonymous;

        public async Task OnGetAsync()
        {
            Access = await HttpContext.AccessAsync();
            var to = (To ?? DateTime.Today).Date;
            var from = (From ?? to.AddDays(-29)).Date;
            if (from > to) (from, to) = (to, from);
            if ((to - from).TotalDays > 366) from = to.AddDays(-366);
            From = from;
            To = to;
            Report = await _reports.LeadsAsync(await _agents.ScopeAsync(Access), from, to);
        }
    }
}
