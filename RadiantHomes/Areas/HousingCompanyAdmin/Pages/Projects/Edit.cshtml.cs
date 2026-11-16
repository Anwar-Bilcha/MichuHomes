using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.Projects
{
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context)
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

            var houseproject =  await _context.Projects.FirstOrDefaultAsync(m => m.ProjectId == id);
            if (houseproject == null)
            {
                return NotFound();
            }
            // Real Estate Admins may only edit their own company's projects
            _scope = await CompanyScope.ForAsync(User, _context);
            if (!_scope.CanAccess(houseproject.HousingCompanyId))
            {
                return Forbid();
            }
            HouseProject = houseproject;
            LoadSelectLists();
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            _scope = await CompanyScope.ForAsync(User, _context);
            var storedCompanyId = await _context.Projects.AsNoTracking()
                .Where(p => p.ProjectId == HouseProject.ProjectId)
                .Select(p => (Guid?)p.HousingCompanyId)
                .FirstOrDefaultAsync();
            if (storedCompanyId == null)
            {
                return NotFound();
            }
            if (!_scope.CanAccess(storedCompanyId))
            {
                return Forbid();
            }
            // ...and the project can't be moved to another company
            if (!_scope.CanAccess(HouseProject.HousingCompanyId))
            {
                ModelState.AddModelError("HouseProject.HousingCompanyId", "Please choose your own company.");
            }

            if (!ModelState.IsValid)
            {
                LoadSelectLists();
                return Page();
            }

            _context.Attach(HouseProject).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HouseProjectExists(HouseProject.ProjectId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        // Set by each handler: the user's company scope
        private CompanyScope _scope = default!;

        // Dropdowns show names, not ids (a Real Estate Admin is only offered their own company)
        private void LoadSelectLists()
        {
            ViewData["HousingCompanyId"] = new SelectList(_scope.Companies(_context.HousingCompanies).OrderBy(c => c.Name), "CompanyId", "Name");
            ViewData["AddressId"] = new SelectList(_context.Addresses.AsEnumerable().OrderBy(a => a.FormattedAddressCompact), "AddressID", "FormattedAddressCompact");
        }

        private bool HouseProjectExists(Guid id)
        {
            return _context.Projects.Any(e => e.ProjectId == id);
        }
    }
}
