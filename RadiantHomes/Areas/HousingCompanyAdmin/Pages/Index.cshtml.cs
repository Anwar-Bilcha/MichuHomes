using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL; // Assuming your ApplicationDbContext is in this namespace
using RadiantHomes.Models;
using System.Linq;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages
{
    //[Authorize(Roles = "HousingCompanyAdmin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<AppUser> _userManager;

        public IndexModel(ApplicationDbContext dbContext, UserManager<AppUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public int ActiveProjectCount { get; set; } = -1; // Initialize to -1 to indicate it hasn't been loaded

        // The signed-in admin's own housing company (null for admins without one, e.g. Michu Admins)
        public Guid? CompanyId { get; set; }

        // Registering another company is a Michu Admin task
        public bool CanCreateCompany { get; set; }

        public async Task OnGetAsync()
        {
            // Fetch the count of active projects from the database (a Real Estate Admin: their own company's)
            var scope = await CompanyScope.ForAsync(User, _dbContext);
            ActiveProjectCount = await scope.Projects(_dbContext.Projects).CountAsync(p => p.EndDate <= DateTime.Now);
            CompanyId = await GetOwnCompanyIdAsync();
            CanCreateCompany = !scope.IsRestricted;
        }

        // AppUser.HousingCompanyId, falling back to the user's Customer profile CompanyId; only if that company exists
        private async Task<Guid?> GetOwnCompanyIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            Guid? companyId = user.HousingCompanyId;
            if ((companyId == null || companyId == Guid.Empty) && user.CustomerId != null)
            {
                companyId = await _dbContext.UserProfiles
                    .Where(p => p.Id == user.CustomerId)
                    .Select(p => p.CompanyId)
                    .FirstOrDefaultAsync();
            }
            if (companyId == null || companyId == Guid.Empty) return null;

            return await _dbContext.HousingCompanies.AnyAsync(c => c.CompanyId == companyId) ? companyId : null;
        }
    }
}