using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Services.AdminServices
{
    public interface IAdminDashboard
    {
        List<HousingCompany> GetHousingCompanies();
        List<Customer> GetAllCustomers();
        Task<IList<AppUser>> GetAllUsers();
        List<ApartmentHouse> GetAllApartmentHouses();
        List<ApartmentHouse> GetSoldHouses(dynamic companyId, DateTime startDate, DateTime endDate, bool isSuperAdmin);
        List<ApartmentHouseSaleTransaction> ApartmentHouseSaleTransactions(bool isSuperAdmin);
    }
}
