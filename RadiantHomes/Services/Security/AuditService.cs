using System.Security.Claims;
using System.Text.Json;
using RadiantHomes.DAL;
using RadiantHomes.Models.Security;

namespace RadiantHomes.Services.Security
{
    public static class AuditCategories
    {
        public const string Users = "Users";
        public const string Roles = "Roles";
        public const string Companies = "Companies";
        public const string Security = "Security";
        public const string Kyc = "KYC";
        public const string Reservations = "Reservations";
        public const string Uploads = "Uploads";
        public const string Settings = "Settings";
    }

    /// <summary>
    /// Writes audit entries. <see cref="Add"/> only stages the entry, so it is saved in the same SaveChanges as the change
    /// it describes (both or neither).
    /// </summary>
    public sealed class AuditService
    {
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _http;

        public AuditService(ApplicationDbContext db, IHttpContextAccessor http)
        {
            _db = db;
            _http = http;
        }

        public AuditLog Add(string category, string action, string summary, Guid? companyId = null,
            string? entityType = null, string? entityId = null, object? details = null)
        {
            var context = _http.HttpContext;
            var user = context?.User;
            var entry = new AuditLog
            {
                OccurredOn = DateTime.UtcNow,
                ActorUserId = user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null,
                ActorName = user?.Identity?.IsAuthenticated == true ? Truncate(user.Identity!.Name, 256) : "System",
                CompanyId = companyId,
                Category = category,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Summary = Truncate(summary, 1000) ?? string.Empty,
                Details = details is null ? null : JsonSerializer.Serialize(details, Json),
                IpAddress = Truncate(context?.Connection.RemoteIpAddress?.ToString(), 64),
            };
            _db.AuditLogs.Add(entry);
            return entry;
        }

        private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
    }
}
