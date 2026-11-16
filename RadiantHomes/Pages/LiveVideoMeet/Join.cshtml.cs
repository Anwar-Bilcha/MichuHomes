using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.DAL;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Services.CustomerPortal;
using RadiantHomes.Services.VideoCalls;

namespace RadiantHomes.Pages.LiveVideoMeet
{
    /// <summary>
    /// An agent answers a customer's video call (/LiveVideoMeet/Join?call=…, the link of the call's notification).
    /// Michu admins join every call, company admins the calls about their company's homes (Program.cs: CompanyAdmin).
    /// ?handler=Waiting lists the unanswered calls for the alert of the admin layout.
    /// </summary>
    public class JoinModel : PageModel
    {
        private readonly VideoCallService _calls;
        private readonly JaasTokens _jaas;
        private readonly ApplicationDbContext _context;
        private readonly PortalUsers _users;

        public JoinModel(VideoCallService calls, JaasTokens jaas, ApplicationDbContext context, PortalUsers users)
        {
            _calls = calls;
            _jaas = jaas;
            _context = context;
            _users = users;
        }

        [BindProperty(SupportsGet = true, Name = "call")]
        public string? Call { get; set; }

        public CallTicket? Ticket { get; private set; }
        public CallView? CallView { get; private set; }

        /// <summary>Why the call can't be opened (expired link, video calls off).</summary>
        public string? Problem { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Ticket = _calls.Open(Call);
            if (Ticket is null)
            {
                Problem = "This call link has expired or is not valid. The customer may have left; you can call them back if they left a phone number.";
                return Page();
            }

            var scope = await CompanyScope.ForAsync(User, _context);
            if (!VideoCallService.CanJoin(scope, Ticket)) return Forbid();

            if (!_jaas.IsConfigured)
            {
                Problem = "Video calls are off: the JaaS keys (Jaas:AppId, Jaas:ApiKeyId and Jaas:PrivateKey) are not set on the server.";
                return Page();
            }

            var me = await _users.GetAsync(User);
            var agentName = me?.Profile is null ? "Michu Homes agent" : $"{me.FullName} (Michu Homes)";
            var agentId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? $"agent-{Ticket.Room}";
            CallView = new CallView(
                IsAgent: true,
                Domain: JaasTokens.Domain,
                ScriptUrl: _jaas.ScriptUrl,
                RoomPath: _jaas.RoomPath(Ticket.Room),
                Jwt: _jaas.CreateToken(Ticket.Room, agentId, agentName, moderator: true, TimeSpan.FromHours(2)),
                Heading: $"Video call with {Ticket.Name}",
                Subheading: null,
                DoneUrl: "/Admin/Notifications/NotificationsAll",
                DoneLabel: "Back to notifications");

            await _calls.MarkAnsweredAsync(Ticket.Room);
            return Page();
        }

        /// <summary>Unanswered calls this agent may join (polled by the admin layout).</summary>
        public async Task<IActionResult> OnGetWaitingAsync()
        {
            var calls = await _calls.WaitingForAsync(User);
            return new JsonResult(calls.Select(c => new { c.Id, c.Name, c.About, c.Url }));
        }
    }
}
