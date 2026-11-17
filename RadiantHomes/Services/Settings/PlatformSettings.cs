using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RadiantHomes.DAL;
using RadiantHomes.Models.Shared;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Services.Settings
{
    /// <summary>A platform setting: key, label, help text, default and allowed range (whole numbers).</summary>
    public sealed record SettingDefinition(string Key, string Label, string Help, int Default, int Min, int Max, string Unit);

    public static class SettingKeys
    {
        public const string ReservationPendingExpiryHours = "Reservations.PendingExpiryHours";

        public static readonly IReadOnlyList<SettingDefinition> All = new[]
        {
            new SettingDefinition(ReservationPendingExpiryHours, "Reservation answer time",
                "How long a new reservation waits for the company to approve it before it expires and the home is listed again. 0 = reservations never expire.",
                72, 0, 24 * 60, "hours"),
        };

        public static SettingDefinition Find(string key) => All.First(d => d.Key == key);
    }

    /// <summary>Platform-wide settings (PlatformSettings table), cached; changed on Admin/Settings and audited.</summary>
    public sealed class PlatformSettings
    {
        private const string CacheKey = "platform-settings";
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly AuditService _audit;

        public PlatformSettings(ApplicationDbContext db, IMemoryCache cache, AuditService audit)
        {
            _db = db;
            _cache = cache;
            _audit = audit;
        }

        public async Task<int> GetIntAsync(string key)
        {
            var definition = SettingKeys.Find(key);
            var values = await AllAsync();
            return values.TryGetValue(key, out var raw) && int.TryParse(raw, out var n) ? Math.Clamp(n, definition.Min, definition.Max) : definition.Default;
        }

        public async Task<IReadOnlyDictionary<string, string>> AllAsync() =>
            await _cache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return (IReadOnlyDictionary<string, string>)await _db.PlatformSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);
            }) ?? new Dictionary<string, string>();

        public async Task<AccessResult> SetIntAsync(UserAccess actor, string key, int value)
        {
            if (!(actor.IsPlatform && actor.Has(Perm.SettingsManage))) return AccessResult.Fail("You don't have permission to change platform settings.");
            var definition = SettingKeys.Find(key);
            if (value < definition.Min || value > definition.Max) return AccessResult.Fail($"{definition.Label} must be between {definition.Min} and {definition.Max} {definition.Unit}.");

            var row = await _db.PlatformSettings.FirstOrDefaultAsync(s => s.Key == key);
            var before = row?.Value ?? definition.Default.ToString();
            if (row is null) _db.PlatformSettings.Add(row = new PlatformSetting { Key = key });
            row.Value = value.ToString();
            row.UpdatedOn = DateTime.UtcNow;
            row.UpdatedBy = actor.UserId;
            _audit.Add(AuditCategories.Settings, "SettingChanged", $"{definition.Label}: {before} → {value} {definition.Unit}.", null, "PlatformSetting", key, new { before, after = value });
            await _db.SaveChangesAsync();
            _cache.Remove(CacheKey);
            return AccessResult.Ok($"{definition.Label} saved.");
        }
    }
}
