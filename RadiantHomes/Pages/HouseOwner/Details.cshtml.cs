using Microsoft.AspNetCore.Authorization;
using RadiantHomes.Services.Security;
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

            // Profiles hold personal data: the owner, or staff with customers.view (platform: any profile;
            // company staff: their own company's profiles).
            var access = await HttpContext.AccessAsync();
            var user = await _userManager.GetUserAsync(User);
            var isOwnProfile = user?.CustomerId == Customer.Id;
            if (!isOwnProfile && !access.Can(Perm.CustomersView, Customer.CompanyId))
            {
                return Forbid();
            }

            return Page();
        }
    }
}
