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

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.Buildings
{
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context)
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

            var building = await _context.Buildings.FirstOrDefaultAsync(m => m.Id == id);
            if (building == null)
            {
                return NotFound();
            }
            // Real Estate Admins may only edit their own company's buildings
            _scope = await CompanyScope.ForAsync(User, _context);
            if (!await _scope.CanAccessBuildingAsync(_context, building.Id))
            {
                return Forbid();
            }
            Building = building;
            LoadSelectLists();
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("Building.Address"); // navigation property; the form posts AddressId

            _scope = await CompanyScope.ForAsync(User, _context);
            if (!await _context.Buildings.AnyAsync(b => b.Id == Building.Id))
            {
                return NotFound();
            }
            if (!await _scope.CanAccessBuildingAsync(_context, Building.Id))
            {
                return Forbid();
            }
            // ...and the building can only be moved to one of the user's own projects
            if (!await _scope.CanAccessProjectAsync(_context, Building.ProjectId))
            {
                ModelState.AddModelError("Building.ProjectId", "Please choose one of your projects.");
            }

            if (!ModelState.IsValid)
            {
                LoadSelectLists();
                return Page();
            }

            _context.Attach(Building).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BuildingExists(Building.Id))
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

        // Dropdowns show names, not ids
        // Set by each handler: the user's company scope
        private CompanyScope _scope = default!;

        private void LoadSelectLists()
        {
            ViewData["ProjectId"] = new SelectList(_scope.Projects(_context.Projects).OrderBy(p => p.ProjectName), "ProjectId", "ProjectName");
            ViewData["AddressId"] = new SelectList(_context.Addresses.AsEnumerable().OrderBy(a => a.FormattedAddressCompact), "AddressID", "FormattedAddressCompact");
        }

        private bool BuildingExists(Guid id)
        {
            return _context.Buildings.Any(e => e.Id == id);
        }
    }
}