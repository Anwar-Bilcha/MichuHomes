using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Services.Properties
{
    /// <summary>
    /// Who may open an apartment's page. Listed apartments (approved, available, approved company) are public. Others
    /// (waiting for approval, rejected, hidden, reserved, sold, or of an unapproved company) only for: platform staff who
    /// review listings, the owning company's staff, and customers who reserved or bought that apartment.
    /// </summary>
    public static class ApartmentVisibility
    {
        public static bool IsListed(ApartmentHouse house) =>
            house.IsAvailable && string.Equals(house.Status, "Approved", StringComparison.OrdinalIgnoreCase)
            && house.Building?.Project?.Company?.Status == CompanyStatuses.Approved;

        public static bool CanManage(UserAccess access, Guid? companyId) =>
            (access.IsPlatform && access.HasAny(Perm.HousesApprove, Perm.HousesUpdate, Perm.CompaniesView))
            || (access.IsCompanyStaff && companyId is not null && access.CompanyId == companyId
                && access.HasAny(Perm.HousesCreate, Perm.HousesUpdate, Perm.HousesDelete, Perm.ReservationsView));

        /// <param name="house">Loaded with Building → Project → Company.</param>
        public static async Task<bool> CanViewAsync(ApplicationDbContext db, UserAccess access, ApartmentHouse house, Guid? customerId)
        {
            if (IsListed(house) || CanManage(access, house.Building?.Project?.HousingCompanyId)) return true;
            return customerId is Guid id && await db.ApartmentHouseSaleTransactions.AnyAsync(t => t.ApartmentHouseId == house.Id && t.CustomerId == id);
        }
    }
}
