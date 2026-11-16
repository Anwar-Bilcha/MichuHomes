using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using System.Threading.Tasks;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public HousingCompany HousingCompany { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            HousingCompany = await _context.HousingCompanies
                .Include(h => h.Address) // Ensure the Address navigation property is loaded
                .FirstOrDefaultAsync(m => m.CompanyId == id);

            if (HousingCompany == null)
            {
                return NotFound();
            }
            // Real Estate Admins may only open their own company
            if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(HousingCompany.CompanyId))
            {
                return Forbid();
            }
            return Page();
        }
    }
}