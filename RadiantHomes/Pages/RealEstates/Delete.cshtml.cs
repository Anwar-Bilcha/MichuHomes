using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Pages.RealEstates
{
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public HousingCompany HousingCompany { get; set; } = default!;
        public List<HouseProject> Projects { get; set; }
        public List<Building> Buildings { get; set; }
        public List<ApartmentHouse> Apartments { get; set; }
        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var housingcompany = await _context.HousingCompanies.FirstOrDefaultAsync(m => m.CompanyId == id);
            Projects = await _context.Projects
            .Where(p => p.HousingCompanyId == id)
            .ToListAsync();

            Buildings = await _context.Buildings
                .Where(b => Projects.Select(p => p.ProjectId).Contains(b.ProjectId))
                .ToListAsync();

            Apartments = await _context.ApartmentHouses
                .Where(a => Buildings.Select(b => b.Id).Contains(a.BuildingId))
                .ToListAsync();

            if (housingcompany is not null)
            {
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
                HousingCompany = housingcompany;
                _context.HousingCompanies.Remove(HousingCompany);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
