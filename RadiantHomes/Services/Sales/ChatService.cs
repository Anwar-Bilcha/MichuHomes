using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Kyc;
using RadiantHomes.Models.Sales;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.CustomerPortal;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Services.Sales
{
    public sealed record MessageDto(long Id, Guid ConversationId, string SenderName, bool FromCustomer, string Body, DateTime SentOn, bool Read, Guid ClientMessageId);

    public sealed record ChatResult(bool Succeeded, string Message, Guid? ConversationId = null, MessageDto? Sent = null)
    {
        public static ChatResult Fail(string message) => new(false, message);
    }

    /// <summary>
    /// Customer ↔ company chat. Only signed-in customers with an approved identity verification start and write; the
    /// conversation is about a home or project of an approved company and goes to that project's agent (or the
    /// company's inbox). Staff read and reply within their sales scope. Messages are stored, delivered live through
    /// ChatHub (pages also work without it), marked read, and the other side is notified.
    /// </summary>
    public sealed class ChatService
    {
        public const int MaxLength = 2000;
        private readonly ApplicationDbContext _db;
        private readonly AgentService _agents;
        private readonly LeadService _leads;
        private readonly CustomerNotifications _customers;
        private readonly IHubContext<ChatHub> _hub;

        public ChatService(ApplicationDbContext db, AgentService agents, LeadService leads, CustomerNotifications customers, IHubContext<ChatHub> hub)
        {
            _db = db;
            _agents = agents;
            _leads = leads;
            _customers = customers;
            _hub = hub;
        }

        public static string Group(Guid conversationId) => "conversation-" + conversationId.ToString("N");

        public Task<bool> IsVerifiedCustomerAsync(string userId) =>
            _db.AppUsers.AnyAsync(u => u.Id == userId && u.AccountType == AccountType.Customer && u.Customer != null && u.Customer.KycStatus == KycStatuses.Approved);

        // ---------- Customer ----------

        public async Task<ChatResult> StartAsync(string customerUserId, Guid? apartmentId, Guid? projectId, string body, Guid clientMessageId)
        {
            var account = await _db.AppUsers.Include(u => u.Customer).FirstOrDefaultAsync(u => u.Id == customerUserId);
            if (account?.Customer is not { } customer || account.AccountType != AccountType.Customer || customer.KycStatus != KycStatuses.Approved)
                return ChatResult.Fail("Only customers with a verified identity can chat with companies.");

            Guid companyId; Guid? project = projectId; string subject;
            if (apartmentId is Guid aid)
            {
                var home = await _db.ApartmentHouses.AsNoTracking().Include(a => a.Building!).ThenInclude(b => b.Project!).ThenInclude(p => p.Company!).FirstOrDefaultAsync(a => a.Id == aid);
                if (home?.Building?.Project?.Company is not { Status: CompanyStatuses.Approved } company) return ChatResult.Fail("This home's company can't be contacted here.");
                companyId = company.CompanyId; project = home.Building.ProjectId; subject = home.Title;
            }
            else if (projectId is Guid pid)
            {
                var p = await _db.Projects.AsNoTracking().Include(x => x.Company).FirstOrDefaultAsync(x => x.ProjectId == pid);
                if (p?.Company is not { Status: CompanyStatuses.Approved }) return ChatResult.Fail("This project's company can't be contacted here.");
                companyId = p.HousingCompanyId; subject = p.ProjectName;
            }
            else return ChatResult.Fail("Choose the home or project you want to ask about.");

            var conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.CustomerUserId == customerUserId && c.CompanyId == companyId
                && c.ApartmentHouseId == apartmentId && c.ProjectId == project);
            if (conversation is null)
            {
                conversation = new Conversation
                {
                    Id = Guid.NewGuid(), CompanyId = companyId, ProjectId = project, ApartmentHouseId = apartmentId, CustomerId = customer.Id,
                    CustomerUserId = customerUserId, Subject = subject.Length > 200 ? subject[..200] : subject, CreatedOn = DateTime.UtcNow, LastMessageOn = DateTime.UtcNow,
                    AgentUserId = await _agents.PickAgentAsync(project),
                };
                _db.Conversations.Add(conversation);
                var lead = await _leads.TouchAsync(companyId, project, apartmentId, customer, "Chat", "Chat", $"Started a chat about {subject}.");
                conversation.LeadId = lead.Id;
                await _db.SaveChangesAsync();
            }
            return await SendAsync(conversation.Id, account, fromCustomer: true, body, clientMessageId, SalesScope.Nobody, UserAccess.Anonymous);
        }

        // ---------- Both sides ----------

        public bool CanRead(Conversation c, string? userId, UserAccess access, SalesScope scope) =>
            (userId is not null && c.CustomerUserId == userId) || (access.Has(Perm.ChatView) && scope.CanSee(c.CompanyId, c.ProjectId, c.AgentUserId));

        public async Task<ChatResult> SendAsync(Guid conversationId, AppUser sender, bool fromCustomer, string? body, Guid clientMessageId, SalesScope scope, UserAccess access)
        {
            var conversation = await _db.Conversations.Include(c => c.Company).FirstOrDefaultAsync(c => c.Id == conversationId);
            if (conversation is null) return ChatResult.Fail("That conversation doesn't exist.");
            if (fromCustomer)
            {
                if (conversation.CustomerUserId != sender.Id) return ChatResult.Fail("That conversation isn't yours.");
                if (!await IsVerifiedCustomerAsync(sender.Id)) return ChatResult.Fail("Your identity verification isn't approved, so you can't send messages.");
            }
            else if (!(access.Has(Perm.ChatReply) && scope.CanSee(conversation.CompanyId, conversation.ProjectId, conversation.AgentUserId)))
            {
                return ChatResult.Fail("You can't reply in this conversation.");
            }
            body = (body ?? string.Empty).Trim();
            if (body.Length == 0) return ChatResult.Fail("Write a message.");
            if (body.Length > MaxLength) return ChatResult.Fail($"Messages can be at most {MaxLength} characters.");
            if (clientMessageId == Guid.Empty) clientMessageId = Guid.NewGuid();

            var existing = await _db.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.ClientMessageId == clientMessageId);
            if (existing is not null) return new ChatResult(true, "Sent.", conversationId, Dto(existing)); // a repeated send

            var name = fromCustomer ? await CustomerNameAsync(sender) : (string.IsNullOrWhiteSpace(sender.FullName) ? conversation.Company.Name : $"{sender.FullName} · {conversation.Company.Name}");
            var message = new ChatMessage
            {
                ConversationId = conversationId, SenderUserId = sender.Id, SenderName = name.Length > 150 ? name[..150] : name, FromCustomer = fromCustomer,
                Body = body, SentOn = DateTime.UtcNow, ClientMessageId = clientMessageId,
            };
            _db.ChatMessages.Add(message);
            conversation.LastMessageOn = message.SentOn;
            if (conversation.Status == ConversationStatuses.Closed && fromCustomer) conversation.Status = ConversationStatuses.Open; // a new question reopens it
            if (!fromCustomer && conversation.AgentUserId is null) conversation.AgentUserId = sender.Id; // the first to answer takes it
            await NotifyAsync(conversation, fromCustomer, body);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                var again = await _db.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.ConversationId == conversationId && m.ClientMessageId == clientMessageId);
                if (again is not null) return new ChatResult(true, "Sent.", conversationId, Dto(again));
                throw;
            }
            var dto = Dto(message);
            await _hub.Clients.Group(Group(conversationId)).SendAsync("message", dto);
            return new ChatResult(true, "Sent.", conversationId, dto);
        }

        /// <summary>The reader saw the other side's messages.</summary>
        public async Task MarkReadAsync(Guid conversationId, bool readerIsCustomer)
        {
            var now = DateTime.UtcNow;
            var changed = await _db.ChatMessages.Where(m => m.ConversationId == conversationId && m.FromCustomer != readerIsCustomer && m.ReadOn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadOn, now));
            if (changed > 0) await _hub.Clients.Group(Group(conversationId)).SendAsync("read", new { conversationId, byCustomer = readerIsCustomer });
        }

        public Task<List<MessageDto>> MessagesAsync(Guid conversationId, long afterId = 0) =>
            _db.ChatMessages.AsNoTracking().Where(m => m.ConversationId == conversationId && m.Id > afterId).OrderBy(m => m.Id).Take(500)
                .Select(m => new MessageDto(m.Id, m.ConversationId, m.SenderName, m.FromCustomer, m.Body, m.SentOn, m.ReadOn != null, m.ClientMessageId)).ToListAsync();

        // ---------- Staff ----------

        public async Task<AccessResult> AssignAsync(UserAccess actor, SalesScope scope, Guid conversationId, string? agentUserId)
        {
            var c = await _db.Conversations.FirstOrDefaultAsync(x => x.Id == conversationId);
            if (c is null || !actor.Has(Perm.ChatAssign) || !scope.CanSee(c.CompanyId, c.ProjectId, c.AgentUserId)) return AccessResult.Fail("You can't assign this conversation.");
            if (agentUserId is not null && !await _db.AppUsers.AnyAsync(u => u.Id == agentUserId && u.AccountType == AccountType.CompanyStaff && u.HousingCompanyId == c.CompanyId))
                return AccessResult.Fail("Choose one of the company's staff.");
            c.AgentUserId = agentUserId;
            await _db.SaveChangesAsync();
            return AccessResult.Ok(agentUserId is null ? "The conversation is back in the company inbox." : "Conversation assigned.");
        }

        public async Task<AccessResult> SetStatusAsync(UserAccess actor, SalesScope scope, Guid conversationId, string status)
        {
            var c = await _db.Conversations.FirstOrDefaultAsync(x => x.Id == conversationId);
            if (c is null || !actor.Has(Perm.ChatReply) || !scope.CanSee(c.CompanyId, c.ProjectId, c.AgentUserId)) return AccessResult.Fail("You can't change this conversation.");
            c.Status = status;
            await _db.SaveChangesAsync();
            return AccessResult.Ok(status == ConversationStatuses.Closed ? "Conversation closed." : "Conversation reopened.");
        }

        private async Task NotifyAsync(Conversation c, bool fromCustomer, string body)
        {
            var preview = body.Length > 120 ? body[..120] + "…" : body;
            if (fromCustomer)
            {
                // One unread alert per conversation is enough.
                var key = c.Id.ToString();
                if (await _db.Notifications.AnyAsync(n => n.DataKey == key && n.Datatype == "Conversations" && !n.IsRead)) return;
                _db.Notifications.Add(new Notification
                {
                    Title = "New customer message", Message = $"{c.Subject}: {preview}", ReceiverCompanyId = c.CompanyId, RecipientRole = MichuHomesRoles.MichuAdmin,
                    Datatype = "Conversations", DataKey = key, RedirectUrl = $"/Staff/Inbox/Conversation?id={c.Id}", CreatedAt = DateTime.Now,
                });
            }
            else
            {
                var key = c.Id.ToString();
                if (await _db.Notifications.AnyAsync(n => n.DataKey == key && n.ReceiverAdminId == c.CustomerId && !n.IsRead)) return;
                _customers.Add(c.CustomerId, $"New message from {c.Company.Name}", preview, EntityAffecteds.UserProfiles, key, $"/Portal/Messages/Conversation?id={c.Id}");
            }
        }

        private async Task<string> CustomerNameAsync(AppUser user)
        {
            var p = user.Customer ?? await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == user.CustomerId);
            return p is null ? user.Email ?? "Customer" : $"{p.FirstName} {p.LastName}".Trim();
        }

        private static MessageDto Dto(ChatMessage m) => new(m.Id, m.ConversationId, m.SenderName, m.FromCustomer, m.Body, m.SentOn, m.ReadOn != null, m.ClientMessageId);
    }

    /// <summary>
    /// Live delivery of chat messages. Every call checks that the connection's user may use the conversation; the hub
    /// only adds speed: pages post the same messages without it.
    /// </summary>
    [Authorize]
    public sealed class ChatHub : Hub
    {
        private readonly ApplicationDbContext _db;
        private readonly ChatService _chat;
        private readonly AccessControlService _access;
        private readonly AgentService _agents;

        public ChatHub(ApplicationDbContext db, ChatService chat, AccessControlService access, AgentService agents)
        {
            _db = db;
            _chat = chat;
            _access = access;
            _agents = agents;
        }

        public async Task<bool> Join(Guid conversationId)
        {
            var (conversation, _, _, _) = await ResolveAsync(conversationId);
            if (conversation is null) return false;
            await Groups.AddToGroupAsync(Context.ConnectionId, ChatService.Group(conversationId));
            return true;
        }

        public async Task<object> Send(Guid conversationId, string body, Guid clientMessageId)
        {
            var (conversation, user, access, scope) = await ResolveAsync(conversationId);
            if (conversation is null || user is null) return new { ok = false, error = "You can't write in this conversation." };
            var result = await _chat.SendAsync(conversationId, user, conversation.CustomerUserId == user.Id, body, clientMessageId, scope, access);
            return result.Succeeded ? new { ok = true, message = result.Sent } : new { ok = false, error = result.Message };
        }

        public async Task MarkRead(Guid conversationId)
        {
            var (conversation, user, _, _) = await ResolveAsync(conversationId);
            if (conversation is not null && user is not null) await _chat.MarkReadAsync(conversationId, conversation.CustomerUserId == user.Id);
        }

        private async Task<(Conversation?, AppUser?, UserAccess, SalesScope)> ResolveAsync(Guid conversationId)
        {
            var userId = Context.UserIdentifier;
            var access = await _access.GetAsync(Context.User);
            var scope = await _agents.ScopeAsync(access);
            var user = userId is null ? null : await _db.AppUsers.Include(u => u.Customer).FirstOrDefaultAsync(u => u.Id == userId);
            var conversation = await _db.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == conversationId);
            if (conversation is null || user is null || !_chat.CanRead(conversation, user.Id, access, scope)) return (null, null, access, scope);
            return (conversation, user, access, scope);
        }
    }
}
