using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Services.CustomerPortal
{
    /// <summary>
    /// The signed-in user of the customer portal and their customer profile. Browsing works without a profile;
    /// reserving, commenting and referring friends need one (Portal/Profile creates it).
    /// </summary>
    public sealed record PortalUser(AppUser Account, Customer? Profile)
    {
        public bool HasProfile => Profile is not null;
        public Guid? CustomerId => Profile?.Id;

        /// <summary>First name, or the e-mail address before a profile exists.</summary>
        public string DisplayName =>
            !string.IsNullOrWhiteSpace(Profile?.FirstName) ? Profile!.FirstName.Trim() : Account.Email ?? Account.UserName ?? string.Empty;

        public string FullName =>
            Profile is null ? DisplayName : $"{Profile.FirstName} {Profile.LastName}".Trim();
    }

    public sealed class PortalUsers
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public PortalUsers(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<PortalUser?> GetAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity?.IsAuthenticated != true) return null;
            var account = await _userManager.GetUserAsync(principal);
            if (account is null) return null;

            Customer? profile = null;
            if (account.CustomerId is Guid id && id != Guid.Empty)
            {
                profile = await _context.UserProfiles.Include(c => c.Address).FirstOrDefaultAsync(c => c.Id == id);
            }
            return new PortalUser(account, profile);
        }

        /// <summary>People who use the portal as customers: not platform or company administrators.</summary>
        public static bool IsCustomer(IList<string> roles) =>
            !roles.Contains(MichuHomesRoles.MichuAdmin) && !roles.Contains(MichuHomesRoles.RealEstateAdmin);
    }
}
