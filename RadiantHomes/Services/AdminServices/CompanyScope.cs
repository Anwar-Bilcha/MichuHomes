using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Services.AdminServices
{
    /// <summary>
    /// Which housing company's data the signed-in user may see and change (tenant scoping).
    /// A Michu Admin sees everything. A Real Estate Admin only sees their own company: AppUser.HousingCompanyId,
    /// falling back to their Customer profile's CompanyId; without a company they see nothing.
    /// Anyone else (visitors on public pages) is not restricted here: public listings stay public, and the
    /// management pages are closed to them by the authorization policies in Program.cs.
    /// </summary>
    public sealed class CompanyScope
    {
        public const string NoCompanyMessage =
            "Your account isn't linked to a housing company yet, so there is nothing to show here. Please contact a Michu Homes administrator.";

        private CompanyScope(bool isMichuAdmin, bool isRealEstateAdmin, Guid? companyId)
        {
            IsMichuAdmin = isMichuAdmin;
            IsRestricted = !isMichuAdmin && isRealEstateAdmin;
            CompanyId = companyId;
        }

        public bool IsMichuAdmin { get; }

        /// <summary>True for a Real Estate Admin (who isn't also a Michu Admin): limited to <see cref="CompanyId"/>.</summary>
        public bool IsRestricted { get; }

        /// <summary>The user's own housing company, if any.</summary>
        public Guid? CompanyId { get; }

        /// <summary>A restricted admin whose account has no company: show <see cref="NoCompanyMessage"/>.</summary>
        public bool HasNoCompany => IsRestricted && CompanyId == null;

        // Used in queries: a restricted admin without a company matches nothing (no company has an empty id).
        private Guid OwnId => CompanyId ?? Guid.Empty;

        public static async Task<CompanyScope> ForAsync(ClaimsPrincipal user, ApplicationDbContext context)
        {
            var isMichuAdmin = user.IsInRole(MichuHomesRoles.MichuAdmin);
            var isRealEstateAdmin = user.IsInRole(MichuHomesRoles.RealEstateAdmin);
            Guid? companyId = null;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null && (isMichuAdmin || isRealEstateAdmin))
            {
                var account = await context.AppUsers
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.HousingCompanyId, u.CustomerId })
                    .FirstOrDefaultAsync();
                companyId = account?.HousingCompanyId is Guid own && own != Guid.Empty ? own : null;
                if (companyId == null && account?.CustomerId is Guid customerId)
                {
                    var profileCompanyId = await context.UserProfiles
                        .Where(p => p.Id == customerId)
                        .Select(p => p.CompanyId)
                        .FirstOrDefaultAsync();
                    companyId = profileCompanyId is Guid fromProfile && fromProfile != Guid.Empty ? fromProfile : null;
                }
            }

            return new CompanyScope(isMichuAdmin, isRealEstateAdmin, companyId);
        }

        /// <summary>Whether the user may see/change a record of this company.</summary>
        public bool CanAccess(Guid? companyId) => !IsRestricted || (CompanyId != null && companyId == CompanyId);

        public IQueryable<HousingCompany> Companies(IQueryable<HousingCompany> query) =>
            IsRestricted ? query.Where(c => c.CompanyId == OwnId) : query;

        public IQueryable<HouseProject> Projects(IQueryable<HouseProject> query) =>
            IsRestricted ? query.Where(p => p.HousingCompanyId == OwnId) : query;

        public IQueryable<Building> Buildings(IQueryable<Building> query) =>
            IsRestricted ? query.Where(b => b.Project != null && b.Project.HousingCompanyId == OwnId) : query;

        public IQueryable<ApartmentHouse> Houses(IQueryable<ApartmentHouse> query) =>
            IsRestricted ? query.Where(h => h.Building != null && h.Building.Project != null && h.Building.Project.HousingCompanyId == OwnId) : query;

        public IQueryable<CompanyFinancialDetail> Financials(IQueryable<CompanyFinancialDetail> query) =>
            IsRestricted ? query.Where(f => f.CompanyId == OwnId) : query;

        public IQueryable<CustomerTestimonial> Testimonials(IQueryable<CustomerTestimonial> query) =>
            IsRestricted ? query.Where(t => t.CompanyId == OwnId) : query;

        // Single-record checks (true when the record exists and the user may access it; false also when it doesn't exist)

        public Task<bool> CanAccessProjectAsync(ApplicationDbContext context, Guid projectId) =>
            Projects(context.Projects).AnyAsync(p => p.ProjectId == projectId);

        public Task<bool> CanAccessBuildingAsync(ApplicationDbContext context, Guid buildingId) =>
            Buildings(context.Buildings).AnyAsync(b => b.Id == buildingId);

        public Task<bool> CanAccessHouseAsync(ApplicationDbContext context, Guid houseId) =>
            Houses(context.ApartmentHouses).AnyAsync(h => h.Id == houseId);
    }
}
