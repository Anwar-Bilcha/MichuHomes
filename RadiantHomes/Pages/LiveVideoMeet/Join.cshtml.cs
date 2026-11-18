using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.Models.Sales;
using RadiantHomes.Services.CustomerPortal;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;
using RadiantHomes.Services.VideoCalls;

namespace RadiantHomes.Pages.LiveVideoMeet
{
    /// <summary>
    /// An agent answers a customer's video call (/LiveVideoMeet/Join?id={call log id}, the link of the call's alert and
    /// notification; older notifications carry ?call={sealed link}). Platform staff join every call, company staff the
    /// calls about their company's homes within their sales scope (assigned projects for agents). Opening the call
    /// marks it answered; leaving it ends it. ?handler=Waiting lists the unanswered calls for the admin layout's alert.
    /// </summary>
    public class JoinModel : PageModel
    {
        private readonly VideoCallService _calls;
        private readonly JaasTokens _jaas;
        private readonly AgentService _agents;
        private readonly PortalUsers _users;

        public JoinModel(VideoCallService calls, JaasTokens jaas, AgentService agents, PortalUsers users)
        {
            _calls = calls;
            _jaas = jaas;
            _agents = agents;
            _users = users;
        }

        [BindProperty(SupportsGet = true)]
        public Guid? Id { get; set; }

        [BindProperty(SupportsGet = true, Name = "call")]
        public string? Call { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? Audio { get; set; }

        public CallLog? Log { get; private set; }
        public CallTicket? Ticket { get; private set; }
        public CallView? CallView { get; private set; }

        /// <summary>Why the call can't be opened (expired link, closed call, video calls off).</summary>
        public string? Problem { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var access = await HttpContext.AccessAsync();
            var scope = await _agents.ScopeAsync(access);

            if (Id is Guid id) Log = await _calls.FindAsync(id);
            else
            {
                Ticket = _calls.Open(Call);
                if (Ticket is not null) Log = await _calls.FindAsync(Ticket.Room);
            }
            if (Log is null && Ticket is null)
            {
                Problem = "This call link has expired or is not valid. The customer may have left; you can call them back if they left a phone number.";
                return Page();
            }
            if (Log is not null ? !VideoCallService.CanJoin(access, scope, Log) : !VideoCallService.CanJoin(access, scope, Ticket!)) return Forbid();

            if (!_jaas.IsConfigured)
            {
                Problem = "Video calls are off: the JaaS keys (Jaas:AppId, Jaas:ApiKeyId and Jaas:PrivateKey) are not set on the server.";
                return Page();
            }

            var me = await _users.GetAsync(User);
            var agentName = me?.Profile is null ? (User.Identity?.Name ?? "Michu Homes agent") : me.FullName;
            var agentId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var room = Log?.Room ?? Ticket!.Room;
            var customer = Log?.CustomerName ?? Ticket!.Name;

            if (Log is not null)
            {
                var entry = await _calls.AgentEnterAsync(Log, agentId, agentName);
                if (!entry.Allowed)
                {
                    Problem = entry.Problem;
                    return Page();
                }
            }
            else
            {
                await _calls.MarkAnsweredAsync(room);
            }

            CallView = new CallView(
                IsAgent: true,
                Domain: JaasTokens.Domain,
                ScriptUrl: _jaas.ScriptUrl,
                RoomPath: _jaas.RoomPath(room),
                Jwt: _jaas.CreateToken(room, agentId, $"{agentName} (Michu Homes)", moderator: true, TimeSpan.FromHours(2)),
                Heading: $"Video call with {customer}",
                Subheading: null,
                DoneUrl: "/Staff/Calls",
                DoneLabel: "Back to calls",
                EndUrl: Log is null ? null : $"/LiveVideoMeet/Join?handler=End&id={Log.Id}",
                AudioOnly: Audio == 1);
            return Page();
        }

        /// <summary>The agent left the call (video-call.js).</summary>
        public async Task<IActionResult> OnPostEndAsync()
        {
            if (Id is not Guid id) return NotFound();
            var access = await HttpContext.AccessAsync();
            var scope = await _agents.ScopeAsync(access);
            var log = await _calls.FindAsync(id);
            if (log is null || !VideoCallService.CanJoin(access, scope, log)) return NotFound();
            await _calls.EndAsync(log, byCustomer: false);
            return new NoContentResult();
        }

        /// <summary>Unanswered calls this agent may join (polled by the admin layout).</summary>
        public async Task<IActionResult> OnGetWaitingAsync()
        {
            var access = await HttpContext.AccessAsync();
            var calls = await _calls.WaitingForAsync(access, await _agents.ScopeAsync(access));
            return new JsonResult(calls.Select(c => new { c.Id, c.Name, c.About, c.Url }));
        }
    }
}
