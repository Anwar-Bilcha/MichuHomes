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

        Dictionary<Guid, int> IAdminDashboard.GetApartmentHouseCountsByCompany()
        {
            return _context.ApartmentHouses
                .Where(a => a.Building != null && a.Building.Project != null)
                .GroupBy(a => a.Building!.Project!.HousingCompanyId)
                .Select(g => new { CompanyId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.CompanyId, x => x.Count);
        }

        // Houses with a sale transaction dated within the range (both dates included)
        List<ApartmentHouse> IAdminDashboard.GetSoldHouses(dynamic companyId, DateTime startDate, DateTime endDate, bool isSuperAdmin)
        {
            var from = startDate.Date;
            var toExclusive = endDate.Date.AddDays(1);
            return _context.ApartmentHouses
                .Where(h => h.SaleTransactions.Any(t => t.TransactionDate >= from && t.TransactionDate < toExclusive))
                .ToList();
        }
    }
}
