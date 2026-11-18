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

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages
{
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DeleteModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public HousingCompany HousingCompany { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var housingcompany = await _context.HousingCompanies
                .Include(m => m.Address)
                .FirstOrDefaultAsync(m => m.CompanyId == id);

            if (housingcompany is not null)
            {
                // Real Estate Admins may only open their own company's records
                if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(housingcompany.CompanyId))
                {
                    return Forbid();
                }
                HousingCompany = housingcompany;

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

            var housingcompany = await _context.HousingCompanies.FindAsync(id);
            if (housingcompany != null)
            {
                if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(housingcompany.CompanyId))
                {
                    return Forbid();
                }
                HousingCompany = housingcompany;
                // The company's access roles don't cascade from it (one cascade path into user role assignments): remove them first.
                var companyRoles = await _context.AccessRoles.Where(r => r.CompanyId == HousingCompany.CompanyId).ToListAsync();
                _context.UserAccessRoles.RemoveRange(_context.UserAccessRoles.Where(ur => ur.Role.CompanyId == HousingCompany.CompanyId));
                _context.AccessRoles.RemoveRange(companyRoles);
                HttpContext.RequestServices.GetRequiredService<RadiantHomes.Services.Security.AuditService>().Add(RadiantHomes.Services.Security.AuditCategories.Companies, "CompanyDeleted", $"Company {HousingCompany.Name} deleted.", null, "HousingCompany", HousingCompany.CompanyId.ToString());
                _context.HousingCompanies.Remove(HousingCompany);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
