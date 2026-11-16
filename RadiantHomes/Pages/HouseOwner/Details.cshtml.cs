using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.HouseOwner
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public DetailsModel(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Customer Customer { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Customer = await _context.UserProfiles
            .Include(c => c.Address)
            .Include(c => c.Ranking).Include(c=>c.RentTransactions).Include(c=>c.SaleTransactions)
            .FirstOrDefaultAsync(m => m.Id == id);

            if (Customer == null)
            {
                return NotFound();
            }

            // Profiles hold personal data: Michu admins see all, real-estate admins see their
            // company's profiles, everyone else only their own.
            if (!User.IsInRole(MichuHomesRoles.MichuAdmin))
            {
                var user = await _userManager.GetUserAsync(User);
                var isOwnProfile = user?.CustomerId == Customer.Id;
                var isOwnCompanyAdmin = User.IsInRole(MichuHomesRoles.RealEstateAdmin)
                    && Customer.CompanyId != null
                    && Customer.CompanyId == user?.HousingCompanyId;
                if (!isOwnProfile && !isOwnCompanyAdmin)
                {
                    return Forbid();
                }
            }

            return Page();
        }
    }
}
