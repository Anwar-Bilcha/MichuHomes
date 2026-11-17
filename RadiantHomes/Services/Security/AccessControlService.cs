using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RadiantHomes.DAL;
using RadiantHomes.Models.Security;

namespace RadiantHomes.Services.Security
{
    /// <summary>A signed-in user's effective access: account type, company, and permissions from their active roles.</summary>
    public sealed class UserAccess
    {
        public static readonly UserAccess Anonymous = new(null, AccountType.Customer, null, null, false, new HashSet<string>());

        public UserAccess(string? userId, AccountType accountType, Guid? companyId, string? companyStatus, bool isActive, IReadOnlySet<string> permissions)
        {
            UserId = userId;
            AccountType = accountType;
            CompanyId = companyId;
            CompanyStatus = companyStatus;
            IsActive = isActive;
            Permissions = permissions;
        }

        public string? UserId { get; }
        public AccountType AccountType { get; }
        /// <summary>Company staff: their company. Platform staff and customers: null.</summary>
        public Guid? CompanyId { get; }
        public string? CompanyStatus { get; }
        public bool IsActive { get; }
        public IReadOnlySet<string> Permissions { get; }

        public bool IsPlatform => AccountType == AccountType.PlatformStaff;
        public bool IsCompanyStaff => AccountType == AccountType.CompanyStaff;
        /// <summary>Active staff with at least one permission.</summary>
        public bool IsStaff => IsActive && (IsPlatform || IsCompanyStaff) && Permissions.Count > 0;
        public bool CompanyApproved => CompanyStatus == CompanyStatuses.Approved;

        public bool Has(string permission) => Permissions.Contains(permission);
        public bool HasAny(params string[] permissions) => permissions.Any(Permissions.Contains);

        /// <summary>
        /// Whether the user may use <paramref name="permission"/> on something that belongs to <paramref name="companyId"/>:
        /// platform staff anywhere, company staff only in their own company.
        /// </summary>
        public bool Can(string permission, Guid? companyId) =>
            Has(permission) && (IsPlatform || (IsCompanyStaff && CompanyId is Guid own && companyId == own));
    }

    /// <summary>
    /// Works out effective permissions (user → active roles of the user's own domain → permissions). Results are cached
    /// for a short time; any change to roles, permissions, assignments, accounts or company status calls
    /// <see cref="Invalidate"/>, so changes apply on the next request.
    /// </summary>
    public sealed class AccessControlService
    {
        private static int _version;
        private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(2);

        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private (string UserId, UserAccess Access)? _memo; // per request

        public AccessControlService(ApplicationDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        /// <summary>Drops every cached permission set (call after any access-related change).</summary>
        public static void Invalidate() => Interlocked.Increment(ref _version);

        public async Task<UserAccess> GetAsync(ClaimsPrincipal? user)
        {
            var userId = user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            if (userId is null) return UserAccess.Anonymous;
            if (_memo is { } memo && memo.UserId == userId) return memo.Access;

            var key = $"access:{Volatile.Read(ref _version)}:{userId}";
            if (!_cache.TryGetValue(key, out UserAccess? access) || access is null)
            {
                access = await LoadAsync(userId);
                _cache.Set(key, access, CacheFor);
            }
            _memo = (userId, access);
            return access;
        }

        public async Task<bool> HasAsync(ClaimsPrincipal? user, string permission) => (await GetAsync(user)).Has(permission);

        public async Task<UserAccess> LoadAsync(string userId)
        {
            var account = await _db.AppUsers.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.AccountType,
                    u.HousingCompanyId,
                    u.LockoutEnabled,
                    u.LockoutEnd,
                    CompanyStatus = u.Company != null ? u.Company.Status : null,
                })
                .FirstOrDefaultAsync();
            if (account is null) return new UserAccess(userId, AccountType.Customer, null, null, false, new HashSet<string>());

            var isActive = !(account.LockoutEnabled && account.LockoutEnd is DateTimeOffset end && end > DateTimeOffset.UtcNow);
            var type = account.AccountType;
            var companyId = type == AccountType.CompanyStaff ? account.HousingCompanyId : null;
            var companyStatus = type == AccountType.CompanyStaff ? account.CompanyStatus : null;
            if (!isActive || type == AccountType.Customer || (type == AccountType.CompanyStaff && companyId is null))
            {
                return new UserAccess(userId, type, companyId, companyStatus, isActive, new HashSet<string>());
            }

            var domain = type == AccountType.PlatformStaff ? PermissionScope.Platform : PermissionScope.Company;
            var keys = await _db.UserAccessRoles.AsNoTracking()
                .Where(ur => ur.UserId == userId && ur.Role.IsActive && ur.Role.Scope == domain
                             && (domain == PermissionScope.Platform ? ur.Role.CompanyId == null : ur.Role.CompanyId == companyId))
                .SelectMany(ur => ur.Role.Permissions.Select(p => p.PermissionKey))
                .Distinct()
                .ToListAsync();

            // Only catalog permissions allowed in the user's domain count (a company role can never carry a platform permission).
            var permissions = new HashSet<string>(
                keys.Where(k => PermissionCatalog.Find(k) is { } def && PermissionCatalog.AllowedIn(def, domain)),
                StringComparer.Ordinal);

            // Company approval gate: staff of a company that isn't approved keep only the profile and inbox.
            if (type == AccountType.CompanyStaff && companyStatus != CompanyStatuses.Approved)
            {
                permissions.IntersectWith(PermissionCatalog.UnapprovedCompanyPermissions);
            }
            return new UserAccess(userId, type, companyId, companyStatus, isActive, permissions);
        }
    }
}
