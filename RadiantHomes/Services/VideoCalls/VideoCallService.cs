using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Sales;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.CustomerPortal;
using RadiantHomes.Services.NotificationService;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Services.VideoCalls
{
    /// <summary>
    /// What a customer's call link carries: the private room, who asked and about what. Sealed with ASP.NET Core data
    /// protection (encrypted, signed, time-limited), so a link can't be forged or reused for another room and the
    /// phone number is not readable in the URL. It only opens for the account that asked (UserId).
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

    public enum CallRequestOutcome { Started, NotConfigured, TooMany, NotVerified }

    public sealed record CallRequestResult(CallRequestOutcome Outcome, string? Token = null);

    /// <summary>A waiting customer, for the agents' alert in the admin area.</summary>
    public sealed record WaitingCall(Guid Id, string Name, string? About, string Url, DateTime CreatedAt);

    /// <summary>Whether a call can be entered, and if not why (the call log's status).</summary>
    public sealed record CallEntry(bool Allowed, CallLog? Log, string? Problem);

    /// <summary>
    /// Video calls with agents (Pages/LiveVideoMeet). A verified customer asks for a call, gets a private room and
    /// waits in it; every request is a CallLog row (waiting → answered → ended, or declined by an agent, missed when
    /// nobody answers in time, cancelled when the customer leaves first). The company's agents for the project (and
    /// platform staff) are alerted and join from the call's link; the customer's page follows the call's status.
    /// </summary>
    public sealed class VideoCallService
    {
        public const int MaxNameLength = 60;
        public const int MaxPhoneLength = 25;
        public const int MaxTopicLength = 300;

        /// <summary>How long a call link works.</summary>
        public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(3);

        /// <summary>A call nobody answered within this time is missed (customer and company are told).</summary>
        public static readonly TimeSpan MissedAfter = TimeSpan.FromMinutes(10);

        /// <summary>Leaving by mistake: the call can be re-entered within this time.</summary>
        public static readonly TimeSpan RejoinWindow = TimeSpan.FromMinutes(15);

        // Requests per visitor (signed-in user or IP address) in RateWindow, so the page can't flood the agents.
        private const int MaxRequests = 3;
        private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(10);

        private static readonly string Datatype = EntityAffecteds.VideoCalls.ToString();

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly ITimeLimitedDataProtector _protector;
        private readonly IMemoryCache _cache;
        private readonly JaasTokens _jaas;
        private readonly LeadService _leads;
        private readonly CustomerNotifications _customers;

        public VideoCallService(ApplicationDbContext context, INotificationService notifications, IDataProtectionProvider protection,
            IMemoryCache cache, JaasTokens jaas, LeadService leads, CustomerNotifications customers)
        {
            _context = context;
            _notifications = notifications;
            _protector = protection.CreateProtector("MichuHomes.VideoCalls.v1").ToTimeLimitedDataProtector();
            _cache = cache;
            _jaas = jaas;
            _leads = leads;
            _customers = customers;
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
        public static string AgentPath(Guid callId) => $"/LiveVideoMeet/Join?id={callId}";

        // ---------- customer: ask for a call ----------

        /// <summary>The account is a customer with an approved identity verification (Models/Kyc).</summary>
        public async Task<bool> IsVerifiedCustomerAsync(string? userId) =>
            userId is not null && await _context.AppUsers.AnyAsync(u => u.Id == userId
                && u.AccountType == AccountType.Customer
                && u.Customer != null && u.Customer.KycStatus == RadiantHomes.Models.Kyc.KycStatuses.Approved);

        /// <summary>Opens a private room, logs the call and tells the agents. <paramref name="visitorKey"/> identifies the visitor for the rate limit.</summary>
        public async Task<CallRequestResult> RequestAsync(CallRequestInput input, string? userId, string visitorKey)
        {
            if (!IsAvailable) return new(CallRequestOutcome.NotConfigured);

            // Only signed-in customers whose identity is verified (KYC approved) may call an agent.
            if (!await IsVerifiedCustomerAsync(userId)) return new(CallRequestOutcome.NotVerified);

            var rateKey = $"videocall-requests:{visitorKey}";
            var count = _cache.GetOrCreate(rateKey, e => { e.AbsoluteExpirationRelativeToNow = RateWindow; return new RequestCounter(); })!;
            if (Interlocked.Increment(ref count.Value) > MaxRequests) return new(CallRequestOutcome.TooMany);

            // The home the customer asks about decides which company (and which project's agents) is told.
            string? apartmentTitle = null;
            Guid? companyId = null, projectId = null;
            if (input.ApartmentId is Guid apartmentId)
            {
                var apartment = await _context.ApartmentHouses.AsNoTracking()
                    .Where(a => a.Id == apartmentId)
                    .Select(a => new { a.Title, ProjectId = (Guid?)a.Building!.ProjectId, CompanyId = (Guid?)a.Building!.Project!.HousingCompanyId })
                    .FirstOrDefaultAsync();
                apartmentTitle = apartment?.Title;
                companyId = apartment?.CompanyId;
                projectId = apartment?.ProjectId;
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

            var log = new CallLog
            {
                Id = Guid.NewGuid(), Room = ticket.Room, CompanyId = companyId, ProjectId = projectId, ApartmentHouseId = ticket.ApartmentId,
                CustomerUserId = userId!, CustomerName = ticket.Name, CustomerPhone = ticket.Phone,
                Topic = ticket.Topic ?? (apartmentTitle is null ? null : $"About \"{apartmentTitle}\""), Status = CallStatuses.Waiting, RequestedOn = DateTime.UtcNow,
            };
            _context.CallLogs.Add(log);
            var customerId = await _context.AppUsers.Where(u => u.Id == userId).Select(u => u.CustomerId).FirstOrDefaultAsync();
            if (companyId is Guid company && customerId is Guid cid && await _context.UserProfiles.FirstOrDefaultAsync(p => p.Id == cid) is { } customer)
                await _leads.TouchAsync(company, projectId, ticket.ApartmentId, customer, "VideoCall", "Call", $"Asked for a video call{(apartmentTitle is null ? "" : $" about \"{apartmentTitle}\"")}.");
            await _context.SaveChangesAsync();

            await _notifications.CreateNotificationAsync(new Notification
            {
                Datatype = Datatype,
                DataKey = ticket.Room,
                ReceiverCompanyId = companyId,
                RecipientRole = MichuHomesRoles.MichuAdmin,
                CreatedAt = DateTime.Now,
                Title = $"{ticket.Name} is waiting for a video call",
                Message = apartmentTitle is null ? "General question about homes." : $"About \"{apartmentTitle}\".",
                RedirectUrl = AgentPath(log.Id),
            });
            return new(CallRequestOutcome.Started, token);
        }

        public Task<CallLog?> FindAsync(string room) => _context.CallLogs.FirstOrDefaultAsync(c => c.Room == room);
        public Task<CallLog?> FindAsync(Guid id) => _context.CallLogs.FirstOrDefaultAsync(c => c.Id == id);

        /// <summary>
        /// The customer opens (or re-opens) their call. A call that ended or was left a moment ago can be re-entered;
        /// a declined or missed one can't.
        /// </summary>
        public async Task<CallEntry> CustomerEnterAsync(CallTicket ticket, string? userId)
        {
            if (ticket.UserId is null || ticket.UserId != userId) return new(false, null, "This call link belongs to another account. Sign in with the account that asked for the call.");
            var log = await FindAsync(ticket.Room);
            if (log is null) return new(true, null, null); // a call from before call logging
            await ExpireIfMissedAsync(log);
            switch (log.Status)
            {
                case CallStatuses.Waiting:
                case CallStatuses.Answered:
                    return new(true, log, null);
                case CallStatuses.Cancelled when log.EndedOn > DateTime.UtcNow - RejoinWindow:
                    log.Status = CallStatuses.Waiting;
                    log.EndedOn = null;
                    await _context.SaveChangesAsync();
                    return new(true, log, null);
                case CallStatuses.Ended when log.EndedOn > DateTime.UtcNow - RejoinWindow:
                    log.Status = CallStatuses.Answered;
                    log.EndedOn = null;
                    await _context.SaveChangesAsync();
                    return new(true, log, null);
                case CallStatuses.Rejected:
                    return new(false, log, "An agent declined this call" + (string.IsNullOrWhiteSpace(log.Reason) ? "." : $": {log.Reason}"));
                case CallStatuses.Missed:
                    return new(false, log, "No agent could answer this call in time. The company was told and may call you back.");
                default:
                    return new(false, log, "This call has ended. You can ask for a new call.");
            }
        }

        /// <summary>The customer's page polls this while waiting (declined or missed calls end the wait).</summary>
        public async Task<CallLog?> StatusForCustomerAsync(CallTicket ticket, string? userId)
        {
            if (ticket.UserId is null || ticket.UserId != userId) return null;
            var log = await FindAsync(ticket.Room);
            if (log is not null) await ExpireIfMissedAsync(log);
            return log;
        }

        // ---------- agents ----------

        /// <summary>Platform staff join every call; company staff the calls about their company's homes within their sales scope.</summary>
        public static bool CanJoin(UserAccess access, SalesScope scope, CallLog log) =>
            access.Has(Perm.VideoCallsReceive) && (scope.IsPlatform || (log.CompanyId is Guid company && scope.CanSee(company, log.ProjectId, log.AgentUserId)));

        /// <summary>Same rule for calls from before call logging (the ticket only knows the company).</summary>
        public static bool CanJoin(UserAccess access, SalesScope scope, CallTicket ticket) =>
            access.Has(Perm.VideoCallsReceive) && (scope.IsPlatform || (ticket.CompanyId is Guid company && scope.CanSee(company, null, null)));

        /// <summary>An agent opens the call: Waiting → Answered (or back in after leaving a moment ago).</summary>
        public async Task<CallEntry> AgentEnterAsync(CallLog log, string agentUserId, string agentName)
        {
            await ExpireIfMissedAsync(log);
            switch (log.Status)
            {
                case CallStatuses.Waiting:
                    log.Status = CallStatuses.Answered;
                    log.AnsweredOn = DateTime.UtcNow;
                    log.AgentUserId = agentUserId;
                    log.AgentName = Clean(agentName, 256);
                    break;
                case CallStatuses.Answered:
                    break;
                case CallStatuses.Ended when log.EndedOn > DateTime.UtcNow - RejoinWindow:
                    log.Status = CallStatuses.Answered;
                    log.EndedOn = null;
                    break;
                case CallStatuses.Cancelled when log.EndedOn > DateTime.UtcNow - RejoinWindow:
                    // The customer left a moment ago (or is reloading the page): the agent still gets in.
                    log.Status = CallStatuses.Answered;
                    log.AnsweredOn = DateTime.UtcNow;
                    log.EndedOn = null;
                    log.AgentUserId = agentUserId;
                    log.AgentName = Clean(agentName, 256);
                    break;
                default:
                    var phone = string.IsNullOrWhiteSpace(log.CustomerPhone) ? "" : $" You can call them back on {log.CustomerPhone}.";
                    return new(false, log, $"This call is closed ({CallStatuses.Label(log.Status)}).{phone}");
            }
            await _context.SaveChangesAsync();
            await MarkAnsweredAsync(log.Room);
            return new(true, log, null);
        }

        /// <summary>An agent declines a waiting call; the customer sees the reason on their call page.</summary>
        public async Task<AccessResult> RejectAsync(UserAccess access, SalesScope scope, Guid callId, string? reason, string? agentName)
        {
            var log = await FindAsync(callId);
            if (log is null || !CanJoin(access, scope, log)) return AccessResult.Fail("You can't answer this call.");
            await ExpireIfMissedAsync(log);
            if (log.Status != CallStatuses.Waiting) return AccessResult.Fail($"The call is no longer waiting ({CallStatuses.Label(log.Status)}).");
            reason = Clean(reason, 500);
            if (reason is null) return AccessResult.Fail("Tell the customer why you can't take the call.");
            log.Status = CallStatuses.Rejected;
            log.Reason = reason;
            log.EndedOn = DateTime.UtcNow;
            log.AgentUserId = access.UserId;
            log.AgentName = Clean(agentName, 256);
            await NotifyCustomerAsync(log, "Video call declined", $"An agent couldn't take your video call: {reason}");
            await _context.SaveChangesAsync();
            await MarkAnsweredAsync(log.Room);
            return AccessResult.Ok("Call declined; the customer was told.");
        }

        /// <summary>Someone left the call page (video-call.js). The customer leaving before an answer cancels the call.</summary>
        public async Task EndAsync(CallLog log, bool byCustomer)
        {
            if (log.Status == CallStatuses.Answered)
            {
                log.Status = CallStatuses.Ended;
                log.EndedOn = DateTime.UtcNow;
            }
            else if (log.Status == CallStatuses.Waiting && byCustomer)
            {
                log.Status = CallStatuses.Cancelled;
                log.EndedOn = DateTime.UtcNow;
                await MarkAnsweredAsync(log.Room);
            }
            else return;
            await _context.SaveChangesAsync();
        }

        /// <summary>Calls nobody answered in time become Missed (called by SalesHousekeepingJob). Returns how many.</summary>
        public async Task<int> ExpireMissedAsync(DateTime? nowUtc = null)
        {
            var cutoff = (nowUtc ?? DateTime.UtcNow) - MissedAfter;
            var due = await _context.CallLogs.Where(c => c.Status == CallStatuses.Waiting && c.RequestedOn <= cutoff).Take(200).ToListAsync();
            foreach (var log in due) await MarkMissedAsync(log);
            if (due.Count > 0) await _context.SaveChangesAsync();
            foreach (var log in due) await MarkAnsweredAsync(log.Room);
            return due.Count;
        }

        /// <summary>Calls waiting for an answer that this agent may join (newest first).</summary>
        public async Task<List<WaitingCall>> WaitingForAsync(UserAccess access, SalesScope scope)
        {
            if (!access.Has(Perm.VideoCallsReceive) || !scope.IsStaff) return new();
            var since = DateTime.UtcNow - MissedAfter;
            var rows = await _context.CallLogs.AsNoTracking().VisibleTo(scope)
                .Where(c => c.Status == CallStatuses.Waiting && c.RequestedOn >= since)
                .OrderByDescending(c => c.RequestedOn).Take(10).ToListAsync();
            if (!scope.IsPlatform) rows = rows.Where(r => r.CompanyId is not null).ToList();
            return rows.Select(r => new WaitingCall(r.Id, r.CustomerName, r.Topic, AgentPath(r.Id), r.RequestedOn)).ToList();
        }

        /// <summary>Nobody else is alerted about this call any more.</summary>
        public Task MarkAnsweredAsync(string room) =>
            _context.Notifications
                .Where(n => n.Datatype == Datatype && n.DataKey == room && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        private async Task ExpireIfMissedAsync(CallLog log)
        {
            if (log.Status != CallStatuses.Waiting || log.RequestedOn > DateTime.UtcNow - MissedAfter) return;
            await MarkMissedAsync(log);
            await _context.SaveChangesAsync();
            await MarkAnsweredAsync(log.Room);
        }

        private async Task MarkMissedAsync(CallLog log)
        {
            log.Status = CallStatuses.Missed;
            log.EndedOn = DateTime.UtcNow;
            log.Reason = "Nobody answered in time.";
            await NotifyCustomerAsync(log, "Missed video call", "Sorry, no agent could answer your video call in time. The company was told and may call you back; you can also send them a message.");
            _context.Notifications.Add(new Notification
            {
                Datatype = "CallLogs", DataKey = log.Id.ToString(), ReceiverCompanyId = log.CompanyId, RecipientRole = MichuHomesRoles.MichuAdmin, CreatedAt = DateTime.Now,
                Title = $"Missed video call from {log.CustomerName}",
                Message = (log.Topic ?? "General question.") + (string.IsNullOrWhiteSpace(log.CustomerPhone) ? "" : $" Call back: {log.CustomerPhone}."),
                RedirectUrl = "/Staff/Calls?status=Missed",
            });
        }

        private async Task NotifyCustomerAsync(CallLog log, string title, string message)
        {
            var customerId = await _context.AppUsers.Where(u => u.Id == log.CustomerUserId).Select(u => u.CustomerId).FirstOrDefaultAsync();
            if (customerId is Guid id && id != Guid.Empty) _customers.Add(id, title, message, EntityAffecteds.VideoCalls, log.Id.ToString(), "/LiveVideoMeet");
        }

        private static string? Clean(string? value, int max)
        {
            var text = string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length == 0 ? null : text.Length > max ? text[..max] : text;
        }

        private sealed class RequestCounter { public int Value; }
    }

    /// <summary>Every minute: unanswered video calls become missed (VideoCallService.ExpireMissedAsync).</summary>
    public sealed class SalesHousekeepingJob : BackgroundService
    {
        public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<SalesHousekeepingJob> _logger;

        public SalesHousekeepingJob(IServiceScopeFactory scopes, ILogger<SalesHousekeepingJob> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var scope = _scopes.CreateScope();
                        var missed = await scope.ServiceProvider.GetRequiredService<VideoCallService>().ExpireMissedAsync();
                        if (missed > 0) _logger.LogInformation("Video calls: {Count} unanswered call(s) marked missed.", missed);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Video calls: marking missed calls failed; trying again in a minute.");
                    }
                    await Task.Delay(Interval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // the app is stopping
            }
        }
    }
}
