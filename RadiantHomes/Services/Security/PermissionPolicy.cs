using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace RadiantHomes.Services.Security
{
    /// <summary>
    /// Authorization policies named "perm:key1|key2" (any of the keys) or "perm:*" (any staff permission), built on demand.
    /// Use <see cref="Perm.Policy"/> to name them.
    /// </summary>
    public static class PermissionPolicy
    {
        public const string Prefix = "perm:";
        public const string Separator = "|";
        public const string AnyStaff = "*";
    }

    public sealed class PermissionRequirement : IAuthorizationRequirement
    {
        public PermissionRequirement(IReadOnlyList<string> keys, bool anyStaff)
        {
            Keys = keys;
            AnyStaff = anyStaff;
        }

        public IReadOnlyList<string> Keys { get; }
        public bool AnyStaff { get; }
    }

    public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallback;

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) => _fallback = new DefaultAuthorizationPolicyProvider(options);

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal)) return _fallback.GetPolicyAsync(policyName);

            var body = policyName[PermissionPolicy.Prefix.Length..];
            var anyStaff = body == PermissionPolicy.AnyStaff;
            var keys = anyStaff ? Array.Empty<string>() : body.Split(PermissionPolicy.Separator, StringSplitOptions.RemoveEmptyEntries);
            var unknown = keys.FirstOrDefault(k => PermissionCatalog.Find(k) is null);
            if (unknown is not null) throw new InvalidOperationException($"Unknown permission '{unknown}' in policy '{policyName}'.");

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(keys, anyStaff))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
    }

    public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly AccessControlService _access;

        public PermissionHandler(AccessControlService access) => _access = access;

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var access = await _access.GetAsync(context.User);
            if (requirement.AnyStaff ? access.IsStaff : access.IsActive && requirement.Keys.Any(access.Has))
            {
                context.Succeed(requirement);
            }
        }
    }
}
