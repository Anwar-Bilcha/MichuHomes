using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Services.NotificationService;

namespace RadiantHomes.Services.VideoCalls
{
    /// <summary>
    /// What a call link carries: the private room, who asked and about what. Sealed with ASP.NET Core data
    /// protection (encrypted, signed, time-limited), so a link can't be forged or reused for another room and the
    /// phone number is not readable in the URL.
    /// </summary>
    public sealed record CallTicket(string Room, string Name, string? Phone, string? Topic, Guid? ApartmentId,
        string? ApartmentTitle, Guid? CompanyId, string? UserId, DateTime CreatedUtc);

    public sealed class CallRequestInput
    {
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Topic { get; set; }
        public Guid? ApartmentId { get; set; }
    }

    public enum CallRequestOutcome { Started, NotConfigured, TooMany }

    public sealed record CallRequestResult(CallRequestOutcome Outcome, string? Token = null);

    /// <summary>A waiting customer, for the agents' alert in the admin area.</summary>
    public sealed record WaitingCall(int Id, string Name, string? About, string Url, DateTime CreatedAt);

    /// <summary>
    /// Video calls with agents (Pages/LiveVideoMeet): a customer asks for a call, gets a private room and waits in it;
    /// Michu admins, and the company admins of the home the call is about, are notified (one notification, like
    /// reservations) and join from its link. No table of its own: the notification is the call request
    /// (Datatype "VideoCalls", DataKey = room) and it is marked read once an agent opens the call.
    /// </summary>
    public sealed class VideoCallService
    {
        public const int MaxNameLength = 60;
        public const int MaxPhoneLength = 25;
        public const int MaxTopicLength = 300;

        /// <summary>How long a call link works.</summary>
        public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(3);

        /// <summary>Calls older than this are no longer shown as "waiting".</summary>
        public static readonly TimeSpan WaitingWindow = TimeSpan.FromMinutes(30);

        // Requests per visitor (signed-in user or IP address) in RateWindow, so the page can't flood the agents.
        private const int MaxRequests = 3;
        private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(10);

        private static readonly string Datatype = EntityAffecteds.VideoCalls.ToString();

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly ITimeLimitedDataProtector _protector;
        private readonly IMemoryCache _cache;
        private readonly JaasTokens _jaas;

        public VideoCallService(ApplicationDbContext context, INotificationService notifications, IDataProtectionProvider protection,
            IMemoryCache cache, JaasTokens jaas)
        {
            _context = context;
            _notifications = notifications;
            _protector = protection.CreateProtector("MichuHomes.VideoCalls.v1").ToTimeLimitedDataProtector();
            _cache = cache;
            _jaas = jaas;
        }

        public bool IsAvailable => _jaas.IsConfigured;

        // ---------- call links ----------

        public string Seal(CallTicket ticket) => _protector.Protect(JsonSerializer.Serialize(ticket), LinkLifetime);

        /// <summary>The call of a link, or null when it is invalid or expired.</summary>
        public CallTicket? Open(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            try
            {
                return JsonSerializer.Deserialize<CallTicket>(_protector.Unprotect(token));
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
            {
                return null;
            }
        }

        public static string CustomerPath(string token) => $"/LiveVideoMeet?call={Uri.EscapeDataString(token)}";
        public static string AgentPath(string token) => $"/LiveVideoMeet/Join?call={Uri.EscapeDataString(token)}";

        // ---------- customer: ask for a call ----------

        /// <summary>Opens a private room and tells the agents. <paramref name="visitorKey"/> identifies the visitor for the rate limit.</summary>
        public async Task<CallRequestResult> RequestAsync(CallRequestInput input, string? userId, string visitorKey)
        {
            if (!IsAvailable) return new(CallRequestOutcome.NotConfigured);

            var rateKey = $"videocall-requests:{visitorKey}";
            var count = _cache.GetOrCreate(rateKey, e => { e.AbsoluteExpirationRelativeToNow = RateWindow; return new RequestCounter(); })!;
            if (Interlocked.Increment(ref count.Value) > MaxRequests) return new(CallRequestOutcome.TooMany);

            // The home the customer asks about decides which company is told (Michu admins always are).
            string? apartmentTitle = null;
            Guid? companyId = null;
            if (input.ApartmentId is Guid apartmentId)
            {
                var apartment = await _context.ApartmentHouses.AsNoTracking()
                    .Where(a => a.Id == apartmentId)
                    .Select(a => new { a.Title, CompanyId = (Guid?)a.Building!.Project!.HousingCompanyId })
                    .FirstOrDefaultAsync();
                apartmentTitle = apartment?.Title;
                companyId = apartment?.CompanyId;
            }

            var ticket = new CallTicket(
                Room: "michu-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant(),
                Name: Clean(input.Name, MaxNameLength)!,
                Phone: Clean(input.Phone, MaxPhoneLength),
                Topic: Clean(input.Topic, MaxTopicLength),
                ApartmentId: apartmentTitle is null ? null : input.ApartmentId,
                ApartmentTitle: apartmentTitle,
                CompanyId: companyId,
                UserId: userId,
                CreatedUtc: DateTime.UtcNow);
            var token = Seal(ticket);

            await _notifications.CreateNotificationAsync(new Notification
            {
                Datatype = Datatype,
                DataKey = ticket.Room,
                ReceiverCompanyId = companyId,
                RecipientRole = MichuHomesRoles.MichuAdmin,
                CreatedAt = DateTime.Now,
                Title = $"{ticket.Name} is waiting for a video call",
                Message = apartmentTitle is null ? "General question about homes." : $"About \"{apartmentTitle}\".",
                RedirectUrl = AgentPath(token),
            });
            return new(CallRequestOutcome.Started, token);
        }

        // ---------- agents ----------

        /// <summary>Michu admins join every call; company admins the calls about their company's homes.</summary>
        public static bool CanJoin(CompanyScope scope, CallTicket ticket) =>
            scope.IsMichuAdmin || (scope.CompanyId is Guid own && ticket.CompanyId == own);

        /// <summary>Calls nobody has answered yet that this agent may join (newest first).</summary>
        public async Task<List<WaitingCall>> WaitingForAsync(ClaimsPrincipal user)
        {
            var scope = await CompanyScope.ForAsync(user, _context);
            if (!scope.IsMichuAdmin && scope.CompanyId is null) return new();

            var since = DateTime.Now - WaitingWindow;
            var query = _context.Notifications.AsNoTracking()
                .Where(n => n.Datatype == Datatype && !n.IsRead && n.CreatedAt >= since);
            if (!scope.IsMichuAdmin) query = query.Where(n => n.ReceiverCompanyId == scope.CompanyId);

            var rows = await query.OrderByDescending(n => n.CreatedAt).Take(10).ToListAsync();
            return rows
                .Select(n => (n, Ticket: Open(TokenOf(n.RedirectUrl))))
                .Where(x => x.Ticket is not null)
                .Select(x => new WaitingCall(x.n.Id, x.Ticket!.Name, x.Ticket.ApartmentTitle, x.n.RedirectUrl!, x.n.CreatedAt))
                .ToList();
        }

        /// <summary>An agent opened the call: it is no longer "waiting" for the other agents.</summary>
        public Task MarkAnsweredAsync(string room) =>
            _context.Notifications
                .Where(n => n.Datatype == Datatype && n.DataKey == room && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        private static string? TokenOf(string? url)
        {
            const string marker = "call=";
            var at = url?.IndexOf(marker, StringComparison.Ordinal) ?? -1;
            return at < 0 ? null : Uri.UnescapeDataString(url![(at + marker.Length)..]);
        }

        private static string? Clean(string? value, int max)
        {
            var text = string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length == 0 ? null : text.Length > max ? text[..max] : text;
        }

        private sealed class RequestCounter { public int Value; }
    }
}
