using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Sales;
using RadiantHomes.Models.Security;
using RadiantHomes.Pages.Shared;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Inbox
{
    /// <summary>A customer conversation: reply (live), assign or transfer, close or reopen, open the customer's lead.</summary>
    public class ConversationModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AgentService _agents;
        private readonly ChatService _chat;
        private readonly SalesReports _reports;

        public ConversationModel(ApplicationDbContext db, AgentService agents, ChatService chat, SalesReports reports)
        {
            _db = db;
            _agents = agents;
            _chat = chat;
            _reports = reports;
        }

        [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
        [BindProperty] public string? Body { get; set; }
        [BindProperty] public Guid ClientMessageId { get; set; }
        [BindProperty] public string? AgentUserId { get; set; }

        public Conversation Conversation { get; private set; } = default!;
        public ChatPanel Panel { get; private set; } = default!;
        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public string? AgentName { get; private set; }
        public List<(string Id, string Name, bool ProjectAgent)> Staff { get; private set; } = new();
        public bool CustomerVerified { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var (conversation, access, scope) = await LoadAsync();
            if (conversation is null) return NotFound();
            await _chat.MarkReadAsync(conversation.Id, readerIsCustomer: false);
            await BuildAsync(conversation, access, scope);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var (conversation, access, scope) = await LoadAsync();
            if (conversation is null) return NotFound();
            var me = await _db.AppUsers.Include(u => u.Customer).FirstAsync(u => u.Id == access.UserId);
            var result = await _chat.SendAsync(conversation.Id, me, fromCustomer: false, Body, ClientMessageId, scope, access);
            if (!result.Succeeded) TempData["Error"] = result.Message;
            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostAssignAsync()
        {
            var (conversation, access, scope) = await LoadAsync();
            if (conversation is null) return NotFound();
            var result = await _chat.AssignAsync(access, scope, Id, string.IsNullOrWhiteSpace(AgentUserId) ? null : AgentUserId);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { id = Id });
        }

        public Task<IActionResult> OnPostCloseAsync() => SetStatus(ConversationStatuses.Closed);
        public Task<IActionResult> OnPostReopenAsync() => SetStatus(ConversationStatuses.Open);

        public async Task<IActionResult> OnGetMessagesAsync(long after = 0)
        {
            var (conversation, _, _) = await LoadAsync();
            if (conversation is null) return NotFound();
            return new JsonResult(await _chat.MessagesAsync(Id, after));
        }

        private async Task<IActionResult> SetStatus(string status)
        {
            var (conversation, access, scope) = await LoadAsync();
            if (conversation is null) return NotFound();
            var result = await _chat.SetStatusAsync(access, scope, Id, status);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { id = Id });
        }

        private async Task<(Conversation?, UserAccess, SalesScope)> LoadAsync()
        {
            var access = await HttpContext.AccessAsync();
            var scope = await _agents.ScopeAsync(access);
            var conversation = await _db.Conversations.AsNoTracking().Include(c => c.Company).Include(c => c.Customer).Include(c => c.Project).Include(c => c.ApartmentHouse)
                .FirstOrDefaultAsync(c => c.Id == Id);
            if (conversation is null || !_chat.CanRead(conversation, access.UserId, access, scope) || conversation.CustomerUserId == access.UserId) return (null, access, scope);
            return (conversation, access, scope);
        }

        private async Task BuildAsync(Conversation c, UserAccess access, SalesScope scope)
        {
            Conversation = c;
            Access = access;
            CustomerVerified = c.Customer.KycStatus == RadiantHomes.Models.Kyc.KycStatuses.Approved;
            var messages = await _chat.MessagesAsync(c.Id);
            string? cannot = null;
            if (!access.Has(Perm.ChatReply)) cannot = "You can read this conversation but your role can't reply.";
            else if (c.Status == ConversationStatuses.Closed) cannot = "This conversation is closed. Reopen it to reply.";
            Panel = new ChatPanel(c.Id, false, $"{c.Customer.FirstName} {c.Customer.LastName}".Trim(), c.Subject, messages,
                Url.Page("/Staff/Inbox/Conversation", new { id = c.Id })!,
                Url.Page("/Staff/Inbox/Conversation", "Messages", new { id = c.Id })!, cannot);

            if (c.AgentUserId is string agent) AgentName = (await _reports.NamesAsync(new[] { agent })).GetValueOrDefault(agent, "Former staff");
            if (access.Has(Perm.ChatAssign))
            {
                var projectAgents = c.ProjectId is Guid pid
                    ? await _db.ProjectAgentAssignments.AsNoTracking().Where(a => a.ProjectId == pid && a.EndedOn == null).Select(a => a.UserId).ToListAsync()
                    : new List<string>();
                var staff = await _db.AppUsers.AsNoTracking().Where(u => u.AccountType == AccountType.CompanyStaff && u.HousingCompanyId == c.CompanyId)
                    .Select(u => u.Id).ToListAsync();
                var names = await _reports.NamesAsync(staff);
                Staff = staff.Select(id => (id, names.GetValueOrDefault(id, id), projectAgents.Contains(id)))
                    .OrderByDescending(s => s.Item3).ThenBy(s => s.Item2).ToList();
            }
        }
    }
}
