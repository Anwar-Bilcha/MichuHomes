using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Sales;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Inbox
{
    /// <summary>Customer conversations within the user's sales scope (agents: their projects and their own chats).</summary>
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

        /// <summary>"open" (default), "mine", "unassigned", "closed" or "all".</summary>
        [BindProperty(SupportsGet = true)] public string? Show { get; set; }

        public string Current => string.IsNullOrEmpty(Show) ? "open" : Show;
        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public List<Row> Items { get; private set; } = new();
        public Dictionary<string, string> Names { get; private set; } = new();
        public int UnreadTotal { get; private set; }

        public sealed record Row(Guid Id, string Subject, string Customer, string Company, string? Project, string? AgentUserId, string Status, DateTime LastMessageOn, string? LastMessage, bool LastFromCustomer, int Unread);

        public async Task OnGetAsync()
        {
            Access = await HttpContext.AccessAsync();
            var scope = await _agents.ScopeAsync(Access);
            var q = _db.Conversations.AsNoTracking().VisibleTo(scope);
            UnreadTotal = await q.SelectMany(c => c.Messages).CountAsync(m => m.FromCustomer && m.ReadOn == null);
            var me = Access.UserId;
            q = Current switch
            {
                "mine" => q.Where(c => c.AgentUserId == me && c.Status == ConversationStatuses.Open),
                "unassigned" => q.Where(c => c.AgentUserId == null && c.Status == ConversationStatuses.Open),
                "closed" => q.Where(c => c.Status == ConversationStatuses.Closed),
                "all" => q,
                _ => q.Where(c => c.Status == ConversationStatuses.Open),
            };
            Items = await q.OrderByDescending(c => c.LastMessageOn).Take(300)
                .Select(c => new Row(c.Id, c.Subject, c.Customer.FirstName + " " + c.Customer.LastName, c.Company.Name, c.Project != null ? c.Project.ProjectName : null,
                    c.AgentUserId, c.Status, c.LastMessageOn,
                    c.Messages.OrderByDescending(m => m.Id).Select(m => m.Body).FirstOrDefault(),
                    c.Messages.OrderByDescending(m => m.Id).Select(m => m.FromCustomer).FirstOrDefault(),
                    c.Messages.Count(m => m.FromCustomer && m.ReadOn == null)))
                .ToListAsync();
            Names = await _reports.NamesAsync(Items.Where(i => i.AgentUserId != null).Select(i => i.AgentUserId!));
        }
    }
}
