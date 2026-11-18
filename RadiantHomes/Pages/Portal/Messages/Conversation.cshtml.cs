using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Kyc;
using RadiantHomes.Models.Sales;
using RadiantHomes.Pages.Shared;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Portal.Messages
{
    /// <summary>One of the customer's conversations: read, write (verified customers), live through chat.js.</summary>
    public class ConversationModel : PortalPageModel
    {
        [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
        [BindProperty] public string? Body { get; set; }
        [BindProperty] public Guid ClientMessageId { get; set; }

        public ChatPanel Panel { get; private set; } = default!;
        public Conversation Conversation { get; private set; } = default!;

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (await LoadAsync() is not { } conversation) return NotFound();
            await Service<ChatService>().MarkReadAsync(conversation.Id, readerIsCustomer: true);
            await BuildAsync(conversation);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (await LoadAsync() is not { } conversation) return NotFound();
            var result = await Service<ChatService>().SendAsync(conversation.Id, Me.Account, fromCustomer: true, Body, ClientMessageId, SalesScope.Nobody, UserAccess.Anonymous);
            if (!result.Succeeded) TempData["Error"] = result.Message;
            return RedirectToPage(new { id = Id });
        }

        /// <summary>Messages after an id (chat.js, when the live connection is down).</summary>
        public async Task<IActionResult> OnGetMessagesAsync(long after = 0)
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (await LoadAsync() is null) return NotFound();
            return new JsonResult(await Service<ChatService>().MessagesAsync(Id, after));
        }

        private Task<Conversation?> LoadAsync()
        {
            var me = Me.Account.Id;
            return Service<ApplicationDbContext>().Conversations.AsNoTracking().Include(c => c.Company)
                .FirstOrDefaultAsync(c => c.Id == Id && c.CustomerUserId == me);
        }

        private async Task BuildAsync(Conversation c)
        {
            Conversation = c;
            var messages = await Service<ChatService>().MessagesAsync(c.Id);
            var verified = Me.Profile?.KycStatus == KycStatuses.Approved;
            Panel = new ChatPanel(c.Id, true, c.Company.Name, c.Subject, messages,
                Url.Page("/Portal/Messages/Conversation", new { id = c.Id })!,
                Url.Page("/Portal/Messages/Conversation", "Messages", new { id = c.Id })!,
                verified ? null : "Your identity verification isn't approved, so you can read this conversation but not write. Verify your identity under Identity verification.");
        }
    }
}
