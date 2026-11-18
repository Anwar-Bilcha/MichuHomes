using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Sales;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Agents
{
    /// <summary>
    /// Sales agents per project: who handles each project's chats, visits and leads (new ones are routed to the least
    /// busy agent), with each agent's open work. Platform staff choose the company.
    /// </summary>
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

        [BindProperty(SupportsGet = true)] public Guid? Company { get; set; }
        [BindProperty] public Guid ProjectId { get; set; }
        [BindProperty] public string? UserId { get; set; }

        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public List<(Guid Id, string Name)> Companies { get; private set; } = new();
        public List<ProjectRow> Projects { get; private set; } = new();
        public List<StaffRow> Staff { get; private set; } = new();
        public Guid? CompanyId { get; private set; }

        public sealed record ProjectRow(Guid Id, string Name, List<(Guid AssignmentId, string UserId, string Name, DateTime Since)> Agents);
        public sealed record StaffRow(string Id, string Name, int Projects, int OpenChats, int OpenLeads, int UpcomingVisits);

        public async Task<IActionResult> OnGetAsync()
        {
            Access = await HttpContext.AccessAsync();
            if (Access.IsPlatform)
            {
                Companies = (await _db.HousingCompanies.AsNoTracking().Where(c => c.Status == CompanyStatuses.Approved).OrderBy(c => c.Name)
                    .Select(c => new { c.CompanyId, c.Name }).ToListAsync()).Select(c => (c.CompanyId, c.Name)).ToList();
                CompanyId = Company ?? Companies.FirstOrDefault().Id;
            }
            else CompanyId = Access.CompanyId;
            if (CompanyId is not Guid company || company == Guid.Empty) return Page();

            var assignments = await _db.ProjectAgentAssignments.AsNoTracking().Where(a => a.Project.HousingCompanyId == company && a.EndedOn == null)
                .Select(a => new { a.Id, a.ProjectId, a.UserId, a.AssignedOn }).ToListAsync();
            var staffIds = await _db.AppUsers.AsNoTracking().Where(u => u.AccountType == AccountType.CompanyStaff && u.HousingCompanyId == company).Select(u => u.Id).ToListAsync();
            var names = await _reports.NamesAsync(staffIds.Concat(assignments.Select(a => a.UserId)));
            var projects = await _db.Projects.AsNoTracking().Where(p => p.HousingCompanyId == company).OrderBy(p => p.ProjectName).Select(p => new { p.ProjectId, p.ProjectName }).ToListAsync();
            Projects = projects.Select(p => new ProjectRow(p.ProjectId, p.ProjectName,
                assignments.Where(a => a.ProjectId == p.ProjectId).Select(a => (a.Id, a.UserId, names.GetValueOrDefault(a.UserId, a.UserId), a.AssignedOn)).OrderBy(a => a.Item3).ToList())).ToList();

            var chats = await _db.Conversations.AsNoTracking().Where(c => c.CompanyId == company && c.Status == ConversationStatuses.Open && c.AgentUserId != null)
                .GroupBy(c => c.AgentUserId!).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N);
            var leads = await _db.Leads.AsNoTracking().Where(l => l.CompanyId == company && l.ClosedOn == null && l.AgentUserId != null)
                .GroupBy(l => l.AgentUserId!).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N);
            var now = DateTime.Now;
            var visits = await _db.SiteVisits.AsNoTracking().Where(v => v.CompanyId == company && v.Status == SiteVisitStatuses.Confirmed && v.ScheduledStart >= now && v.AgentUserId != null)
                .GroupBy(v => v.AgentUserId!).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N);
            Staff = staffIds.Select(id => new StaffRow(id, names.GetValueOrDefault(id, id), assignments.Count(a => a.UserId == id),
                chats.GetValueOrDefault(id), leads.GetValueOrDefault(id), visits.GetValueOrDefault(id))).OrderBy(s => s.Name).ToList();
            return Page();
        }

        public async Task<IActionResult> OnPostAssignAsync()
        {
            var access = await HttpContext.AccessAsync();
            var result = string.IsNullOrWhiteSpace(UserId) ? AccessResult.Fail("Choose a staff member.") : await _agents.AssignAsync(access, ProjectId, UserId, User.Identity?.Name);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { company = Company });
        }

        public async Task<IActionResult> OnPostEndAsync(Guid id)
        {
            var access = await HttpContext.AccessAsync();
            var result = await _agents.EndAsync(access, id, User.Identity?.Name);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { company = Company });
        }
    }
}
