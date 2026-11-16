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

        public ApartmentCard? About { get; private set; }
        public CallView? CallView { get; private set; }
        public bool VideoAvailable => _calls.IsAvailable;
        public bool LinkExpired { get; private set; }

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
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? $"guest-{ticket.Room}";
                    CallView = new CallView(
                        IsAgent: false,
                        Domain: JaasTokens.Domain,
                        ScriptUrl: _jaas.ScriptUrl,
                        RoomPath: _jaas.RoomPath(ticket.Room),
                        Jwt: _jaas.CreateToken(ticket.Room, userId, ticket.Name, moderator: false, VideoCallService.LinkLifetime),
                        Heading: "Video call with Michu Homes",
                        Subheading: ticket.ApartmentTitle is null ? null : $"About \"{ticket.ApartmentTitle}\"",
                        DoneUrl: ticket.ApartmentId is Guid id ? $"/ApartmentHouses/Details?id={id}" : "/ApartmentHouses",
                        DoneLabel: ticket.ApartmentId is null ? "Browse homes" : "Back to the home");
                    return Page();
                }
                LinkExpired = true;
            }

            await LoadAboutAsync();
            await PrefillAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadAboutAsync();
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
