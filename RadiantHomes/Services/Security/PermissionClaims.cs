using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace RadiantHomes.Services.Security
{
    /// <summary>
    /// Adds the signed-in user's effective permissions to the request's principal (never to the cookie), so synchronous
    /// code can check <c>User.HasPermission(...)</c>. Permission claims arriving with the request are removed first:
    /// only what the server computed counts.
    /// </summary>
    public sealed class PermissionClaimsTransformation : IClaimsTransformation
    {
        public const string ClaimType = "mh:permission";
        private const string IdentityType = "MichuHomes.Permissions";
        private readonly AccessControlService _access;

        public PermissionClaimsTransformation(AccessControlService access) => _access = access;

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity?.IsAuthenticated != true) return principal;

            var access = await _access.GetAsync(principal);
            var result = new ClaimsPrincipal();
            foreach (var identity in principal.Identities)
            {
                if (identity.AuthenticationType == IdentityType) continue; // added by an earlier pass in this request
                var copy = identity.Clone();
                foreach (var claim in copy.FindAll(ClaimType).ToList()) copy.TryRemoveClaim(claim);
                result.AddIdentity(copy);
            }
            var permissions = new ClaimsIdentity(IdentityType);
            foreach (var key in access.Permissions) permissions.AddClaim(new Claim(ClaimType, key));
            result.AddIdentity(permissions);
            return result;
        }
    }

    public static class PermissionClaimsExtensions
    {
        /// <summary>Whether the signed-in user holds the permission (from any of their active roles).</summary>
        public static bool HasPermission(this ClaimsPrincipal user, string permission) =>
            user.Identities.Any(i => i.AuthenticationType == "MichuHomes.Permissions" && i.HasClaim(PermissionClaimsTransformation.ClaimType, permission));
    }
}
