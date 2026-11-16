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

namespace RadiantHomes.Pages.Projects
{
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public HouseProject HouseProject { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houseproject = await _context.Projects.Include(p => p.Company).FirstOrDefaultAsync(m => m.ProjectId == id);

            if (houseproject is not null)
            {
                // Real Estate Admins may only delete their own company's projects
                if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(houseproject.HousingCompanyId))
                {
                    return Forbid();
                }
                HouseProject = houseproject;

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

            var houseproject = await _context.Projects.FindAsync(id);
            if (houseproject != null)
            {
                if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(houseproject.HousingCompanyId))
                {
                    return Forbid();
                }
                HouseProject = houseproject;
                _context.Projects.Remove(HouseProject);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
