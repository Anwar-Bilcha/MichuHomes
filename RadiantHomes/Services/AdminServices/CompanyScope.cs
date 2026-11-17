using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Security;

namespace RadiantHomes.Services.AdminServices
{
    /// <summary>
    /// Which housing company's data the signed-in user may see and change (tenant scoping). What they may DO is decided
    /// by permissions (Services/Security); this only decides WHERE.
    /// Platform staff see every company. Company staff only see their own company (AppUser.HousingCompanyId, taken
    /// from the signed-in account, never from a submitted value); without a company they see nothing.
    /// Anyone else (visitors on public pages) is not restricted here: public listings stay public, and the
    /// management pages are closed to them by the permission policies in Program.cs.
    /// </summary>
    public sealed class CompanyScope
    {
        public const string NoCompanyMessage =
            "Your account isn't linked to a housing company yet, so there is nothing to show here. Please contact a Michu Homes administrator.";

        private CompanyScope(bool isPlatform, bool isCompanyStaff, Guid? companyId)
        {
            IsMichuAdmin = isPlatform;
            IsRestricted = !isPlatform && isCompanyStaff;
            CompanyId = companyId;
        }

        /// <summary>Platform (Michu Homes) staff: not limited to one company.</summary>
        public bool IsMichuAdmin { get; }

        /// <summary>True for company staff: limited to <see cref="CompanyId"/>.</summary>
        public bool IsRestricted { get; }

        /// <summary>The user's own housing company, if any.</summary>
        public Guid? CompanyId { get; }

        /// <summary>A restricted admin whose account has no company: show <see cref="NoCompanyMessage"/>.</summary>
        public bool HasNoCompany => IsRestricted && CompanyId == null;

        // Used in queries: a restricted admin without a company matches nothing (no company has an empty id).
        private Guid OwnId => CompanyId ?? Guid.Empty;

        public static async Task<CompanyScope> ForAsync(ClaimsPrincipal user, ApplicationDbContext context)
        {
            var userId = user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
            if (userId is null) return new CompanyScope(false, false, null);

            var account = await context.AppUsers.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.AccountType, u.HousingCompanyId })
                .FirstOrDefaultAsync();
            var isPlatform = account?.AccountType == AccountType.PlatformStaff;
            var isCompanyStaff = account?.AccountType == AccountType.CompanyStaff;
            var companyId = isCompanyStaff && account!.HousingCompanyId is Guid own && own != Guid.Empty ? own : (Guid?)null;
            return new CompanyScope(isPlatform, isCompanyStaff, companyId);
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
