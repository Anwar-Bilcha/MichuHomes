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

namespace RadiantHomes.Pages.Buildings
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

        /// <summary>Set when the saved type/status text isn't one of the options (e.g. old free-text values).</summary>
        public string? UnknownValuesNote { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var building = await _context.Buildings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (building == null)
            {
                return NotFound();
            }
            // Real Estate Admins may only edit their own company's buildings
            var scope = await CompanyScope.ForAsync(User, _context);
            if (!await scope.CanAccessBuildingAsync(_context, building.Id))
            {
                return Forbid();
            }
            Building = building;
            UnknownValuesNote = BuildingOptions.NormaliseForForm(Building);
            PopulateProjects(scope);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Building == null)
            {
                return BadRequest();
            }

            var existing = await _context.Buildings.FirstOrDefaultAsync(b => b.Id == Building.Id);
            if (existing == null)
            {
                return NotFound();
            }
            var scope = await CompanyScope.ForAsync(User, _context);
            if (!await scope.CanAccessBuildingAsync(_context, existing.Id))
            {
                return Forbid();
            }

            BuildingOptions.Validate(ModelState, Building);
            ModelState.Remove($"{nameof(Building)}.{nameof(Building.ProjectId)}");
            // The building can only be moved to one of the user's own projects
            var project = await scope.Projects(_context.Projects).AsNoTracking().FirstOrDefaultAsync(p => p.ProjectId == Building.ProjectId);
            if (project == null)
            {
                ModelState.AddModelError($"{nameof(Building)}.{nameof(Building.ProjectId)}", "Please select the project this building belongs to.");
            }

            if (!ModelState.IsValid)
            {
                PopulateProjects(scope);
                return Page();
            }

            // Only the edited fields change; the address follows the project (as on Create) and the audit fields
            // are maintained by ApplicationDbContext.
            if (existing.ProjectId != project!.ProjectId)
            {
                existing.ProjectId = project.ProjectId;
                existing.AddressId = project.AddressId;
            }
            existing.BuildingName = Building.BuildingName;
            existing.NumberOfFloors = Building.NumberOfFloors;
            existing.BuildingType = Building.BuildingType;
            existing.ConstructionStatus = Building.ConstructionStatus;

            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        private void PopulateProjects(CompanyScope scope) => ViewData["ProjectId"] = CreateModel.ProjectSelectList(_context, scope, Building?.ProjectId);
    }
}
