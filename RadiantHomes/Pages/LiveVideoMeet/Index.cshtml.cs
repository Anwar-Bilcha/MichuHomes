using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.Services.CustomerPortal;
using RadiantHomes.Services.VideoCalls;

namespace RadiantHomes.Pages.LiveVideoMeet
{
    /// <summary>
    /// Video call with an agent (/LiveVideoMeet, optionally ?apartment={id} from a home's page): the customer gives a
    /// name and asks for a call; the agents are told and the customer waits in a private room (?call={sealed link}).
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly VideoCallService _calls;
        private readonly JaasTokens _jaas;
        private readonly ApartmentCatalog _catalog;
        private readonly PortalUsers _users;

        public IndexModel(VideoCallService calls, JaasTokens jaas, ApartmentCatalog catalog, PortalUsers users)
        {
            _calls = calls;
            _jaas = jaas;
            _catalog = catalog;
            _users = users;
        }

        /// <summary>The home the customer wants to talk about (optional).</summary>
        [BindProperty(SupportsGet = true)]
        public Guid? Apartment { get; set; }

        /// <summary>The sealed call link, once the call was requested.</summary>
        [BindProperty(SupportsGet = true, Name = "call")]
        public string? Call { get; set; }

        [BindProperty]
        public CallInput Input { get; set; } = new();

        /// <summary>"Audio only": the call starts without the camera.</summary>
        [BindProperty(SupportsGet = true)]
        public int? Audio { get; set; }

        public ApartmentCard? About { get; private set; }
        public CallView? CallView { get; private set; }
        public bool VideoAvailable => _calls.IsAvailable;
        public bool LinkExpired { get; private set; }

        /// <summary>The call link is valid but the call can't be entered (declined, missed, ended, another account).</summary>
        public string? CallClosed { get; private set; }

        /// <summary>What the visitor still needs before calling: "signin", "profile", "verify", or "ok" (KYC approved).</summary>
        public string Eligibility { get; private set; } = "signin";
        public string? KycStatus { get; private set; }

        private async Task LoadEligibilityAsync()
        {
            if (User.Identity?.IsAuthenticated != true) { Eligibility = "signin"; return; }
            var me = await _users.GetAsync(User);
            if (me?.Profile is null) { Eligibility = "profile"; return; }
            KycStatus = me.Profile.KycStatus;
            Eligibility = await _calls.IsVerifiedCustomerAsync(me.Account.Id) ? "ok" : "verify";
        }

        public sealed class CallInput
        {
            [Required(ErrorMessage = "Please enter your name.")]
            [StringLength(VideoCallService.MaxNameLength, MinimumLength = 2, ErrorMessage = "Please enter a name of 2 to 60 characters.")]
            [Display(Name = "Your name")]
            public string Name { get; set; } = string.Empty;

            [Phone(ErrorMessage = "Please enter a valid phone number.")]
            [StringLength(VideoCallService.MaxPhoneLength, ErrorMessage = "Please enter a valid phone number.")]
            [Display(Name = "Phone number (optional)")]
            public string? Phone { get; set; }

            [StringLength(VideoCallService.MaxTopicLength, ErrorMessage = "Please keep it under 300 characters.")]
            [Display(Name = "What would you like to talk about? (optional)")]
            public string? Topic { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!string.IsNullOrEmpty(Call))
            {
                var ticket = _calls.Open(Call);
                if (ticket is not null && _jaas.IsConfigured)
                {
                    // The link only opens for the account that asked for the call.
                    if (User.Identity?.IsAuthenticated != true) return Challenge();
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
                    var entry = await _calls.CustomerEnterAsync(ticket, userId);
                    if (!entry.Allowed)
                    {
                        CallClosed = entry.Problem;
                        await LoadAboutAsync();
                        await LoadEligibilityAsync();
                        await PrefillAsync();
                        return Page();
                    }
                    var token = Uri.EscapeDataString(Call);
                    CallView = new CallView(
                        IsAgent: false,
                        Domain: JaasTokens.Domain,
                        ScriptUrl: _jaas.ScriptUrl,
                        RoomPath: _jaas.RoomPath(ticket.Room),
                        Jwt: _jaas.CreateToken(ticket.Room, userId, ticket.Name, moderator: false, VideoCallService.LinkLifetime),
                        Heading: "Video call with Michu Homes",
                        Subheading: ticket.ApartmentTitle is null ? null : $"About \"{ticket.ApartmentTitle}\"",
                        DoneUrl: ticket.ApartmentId is Guid id ? $"/ApartmentHouses/Details?id={id}" : "/ApartmentHouses",
                        DoneLabel: ticket.ApartmentId is null ? "Browse homes" : "Back to the home",
                        EndUrl: entry.Log is null ? null : $"/LiveVideoMeet?handler=End&call={token}",
                        StatusUrl: entry.Log is null ? null : $"/LiveVideoMeet?handler=Status&call={token}",
                        AudioOnly: Audio == 1);
                    return Page();
                }
                LinkExpired = true;
            }

            await LoadAboutAsync();
            await LoadEligibilityAsync();
            await PrefillAsync();
            return Page();
        }

        /// <summary>Polled by the waiting customer's page: a declined or missed call ends the wait.</summary>
        public async Task<IActionResult> OnGetStatusAsync()
        {
            var ticket = _calls.Open(Call);
            var log = ticket is null ? null : await _calls.StatusForCustomerAsync(ticket, User.FindFirstValue(ClaimTypes.NameIdentifier));
            if (log is null) return NotFound();
            return new JsonResult(new { status = log.Status, reason = log.Status == RadiantHomes.Models.Sales.CallStatuses.Rejected ? log.Reason : null });
        }

        /// <summary>The customer left the call (video-call.js).</summary>
        public async Task<IActionResult> OnPostEndAsync()
        {
            var ticket = _calls.Open(Call);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (ticket is null || userId is null || ticket.UserId != userId) return NotFound();
            var log = await _calls.FindAsync(ticket.Room);
            if (log is not null) await _calls.EndAsync(log, byCustomer: true);
            return new NoContentResult();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadAboutAsync();
            await LoadEligibilityAsync();
            if (Eligibility != "ok")
            {
                ModelState.AddModelError(string.Empty, "Only signed-in customers with a verified identity can video call agents.");
                return Page();
            }
            if (!ModelState.IsValid) return Page();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var visitor = userId ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _calls.RequestAsync(new CallRequestInput
            {
                Name = Input.Name,
                Phone = Input.Phone,
                Topic = Input.Topic,
                ApartmentId = About?.Id,
            }, userId, visitor);

            switch (result.Outcome)
            {
                case CallRequestOutcome.Started:
                    return Redirect(VideoCallService.CustomerPath(result.Token!));
                case CallRequestOutcome.TooMany:
                    ModelState.AddModelError(string.Empty, "You have asked for several calls in a short time. Please wait a few minutes and try again, or call us.");
                    return Page();
                case CallRequestOutcome.NotVerified:
                    ModelState.AddModelError(string.Empty, "Only signed-in customers with a verified identity can video call agents.");
                    return Page();
                default:
                    ModelState.AddModelError(string.Empty, "Video calls are not available at the moment. Please call us or use the chat.");
                    return Page();
            }
        }

        private async Task LoadAboutAsync()
        {
            if (Apartment is not Guid id) return;
            var apartment = await _catalog.FindAsync(id);
            About = apartment is null ? null : _catalog.ToCards(new[] { apartment }).FirstOrDefault();
        }

        /// <summary>Signed-in customers don't retype their name and phone.</summary>
        private async Task PrefillAsync()
        {
            if (User.Identity?.IsAuthenticated != true) return;
            var me = await _users.GetAsync(User);
            if (me?.Profile is { } profile)
            {
                Input.Name = me.FullName;
                Input.Phone = profile.PhoneNumber;
            }
        }
    }
}
