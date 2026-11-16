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

namespace RadiantHomes.Pages.Projects
{
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }
        /// <summary>The project's company (shown on the page; it isn't edited here).</summary>
        public HousingCompany Company { get; set; } = new();

        [BindProperty]
        public HouseProject HouseProject { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var houseproject = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(m => m.ProjectId == id);

            if (houseproject == null)
            {
                return NotFound();
            }
            // Real Estate Admins may only edit their own company's projects
            if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(houseproject.HousingCompanyId))
            {
                return Forbid();
            }
            Company = await _context.HousingCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == houseproject.HousingCompanyId) ?? new();

            HouseProject = houseproject;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (HouseProject == null)
            {
                return BadRequest();
            }

            var existing = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == HouseProject.ProjectId);
            if (existing == null)
            {
                return NotFound();
            }
            if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(existing.HousingCompanyId))
            {
                return Forbid();
            }

            // Company and address aren't on this form (nor their navigations): they keep their saved values.
            foreach (var key in new[] { nameof(HouseProject.Company), nameof(HouseProject.Address), nameof(HouseProject.HousingCompanyId),
                                        nameof(HouseProject.AddressId), nameof(HouseProject.Buildings), nameof(HouseProject.Photos) })
            {
                ModelState.Remove($"{nameof(HouseProject)}.{key}");
            }
            if (HouseProject.EndDate is DateTime end && end < HouseProject.StartDate)
            {
                ModelState.AddModelError($"{nameof(HouseProject)}.{nameof(HouseProject.EndDate)}", "End Date cannot be before the Start Date.");
            }

            if (!ModelState.IsValid)
            {
                Company = await _context.HousingCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == existing.HousingCompanyId) ?? new();
                return Page();
            }

            // UpdatedOn/UpdatedBy are stamped by ApplicationDbContext.
            existing.ProjectName = HouseProject.ProjectName;
            existing.Location = HouseProject.Location;
            existing.TotalArea = HouseProject.TotalArea;
            existing.StartDate = HouseProject.StartDate;
            existing.EndDate = HouseProject.EndDate;
            existing.Description = HouseProject.Description;

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

        private bool HouseProjectExists(Guid id)
        {
            return _context.Projects.Any(e => e.ProjectId == id);
        }
    }
}
