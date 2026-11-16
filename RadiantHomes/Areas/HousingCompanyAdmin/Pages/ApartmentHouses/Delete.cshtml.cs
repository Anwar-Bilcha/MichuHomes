using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.ApartmentHouses
{
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [BindProperty]
        public ApartmentHouse ApartmentHouse { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var apartmenthouse = await _context.ApartmentHouses
                .Include(m => m.HouseCategory)
                .Include(m => m.HouseAddress)
                .Include(m => m.Building).ThenInclude(b => b!.Project).ThenInclude(p => p!.Company)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (apartmenthouse is not null)
            {
                // Real Estate Admins may only open their own company's records
                if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(apartmenthouse.Building?.Project?.HousingCompanyId))
                {
                    return Forbid();
                }
                ApartmentHouse = apartmenthouse;

                return Page();
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var apartmenthouse = await _context.ApartmentHouses.FindAsync(id);
            if (apartmenthouse != null)
            {
                if (!await (await CompanyScope.ForAsync(User, _context)).CanAccessHouseAsync(_context, apartmenthouse.Id))
                {
                    return Forbid();
                }
                ApartmentHouse = apartmenthouse;
                _context.ApartmentHouses.Remove(ApartmentHouse);
                await _context.SaveChangesAsync();
                // Its 3D model is a file on disk (see ApartmentModel3D), removed once the record is gone.
                RadiantHomes.Pages.ApartmentHouses.ApartmentModel3D.Delete(_env, ApartmentHouse.Id);
            }

            return RedirectToPage("./Index");
        }
    }
}
