using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
namespace RadiantHomes.Services.AdminServices
{
    public class AdminDashboardService : IAdminDashboard
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public AdminDashboardService(ApplicationDbContext dbContext, UserManager<AppUser> userManager)
        {
            _context=dbContext;
            _userManager=userManager;
        }
        List<ApartmentHouseSaleTransaction> IAdminDashboard.ApartmentHouseSaleTransactions(bool isSuperAdmin)
        {
            List<ApartmentHouseSaleTransaction> response = new List<ApartmentHouseSaleTransaction>();
           if(isSuperAdmin)
            {
                response=_context.ApartmentHouseSaleTransactions.ToList();
                return response;
            }
           return response;
        }

        List<ApartmentHouse> IAdminDashboard.GetAllApartmentHouses()
        {
            return _context.ApartmentHouses.ToList();
        }

        List<Customer> IAdminDashboard.GetAllCustomers()
        {
            return _context.UserProfiles.ToList();
        }

        async Task<IList<AppUser>> IAdminDashboard.GetAllUsers()
        {
        var result = await _userManager.GetUsersInRoleAsync(MichuHomesRoles.Agent);
        return result;
        }

        List<HousingCompany> IAdminDashboard.GetHousingCompanies()
        {
            return _context.HousingCompanies.ToList();
        }

        List<ApartmentHouse> IAdminDashboard.GetSoldHouses(dynamic companyId, DateTime startDate, DateTime endDate, bool isSuperAdmin)
        {
            var apartmentSaleTransactions = _context.ApartmentHouseSaleTransactions.ToList().Select(a=>a.ApartmentHouseId);
        var result = _context.ApartmentHouses.Where(a => apartmentSaleTransactions.Contains(a.Id) && a.CreatedOn >= startDate && a.CreatedOn <= endDate);
        return result.ToList();
        }
    }
}
