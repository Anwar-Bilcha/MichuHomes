using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Services.AdminServices
{
    public interface IAdminDashboard
    {
        List<HousingCompany> GetHousingCompanies();
        /// <summary>Number of apartment houses per housing company (house → building → project → company). Companies without houses are absent.</summary>
        Dictionary<Guid, int> GetApartmentHouseCountsByCompany();
        List<Customer> GetAllCustomers();
        Task<IList<AppUser>> GetAllUsers();
        List<ApartmentHouse> GetAllApartmentHouses();
        List<ApartmentHouse> GetSoldHouses(dynamic companyId, DateTime startDate, DateTime endDate, bool isSuperAdmin);
        List<ApartmentHouseSaleTransaction> ApartmentHouseSaleTransactions(bool isSuperAdmin);
    }
}
