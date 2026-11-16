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

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.Buildings
{
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Building Building { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var building = await _context.Buildings
                .Include(m => m.Project).ThenInclude(p => p!.Company)
                .Include(m => m.Address)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (building is not null)
            {
                // Real Estate Admins may only open their own company's records
                if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(building.Project?.HousingCompanyId))
                {
                    return Forbid();
                }
                Building = building;

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

            var building = await _context.Buildings.FindAsync(id);
            if (building != null)
            {
                if (!await (await CompanyScope.ForAsync(User, _context)).CanAccessBuildingAsync(_context, building.Id))
                {
                    return Forbid();
                }
                Building = building;
                _context.Buildings.Remove(Building);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
