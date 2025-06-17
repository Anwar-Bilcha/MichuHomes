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
    public class DetailsModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context)
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
                .Include(a=>a.CustomerTestimonials).
                Include(a=>a.Projects).ThenInclude(a=>a.Buildings).ThenInclude(a=>a.Houses)
                .Include(a=>a.Address)
                .Include(a=>a.FinancialDetails).FirstOrDefaultAsync(m => m.CompanyId == id)??new();
            HousingCompany.ContactPerson = _context.UserProfiles.ToList()??new();
            if (HousingCompany is not null)
            {
                HousingCompany = HousingCompany;

                return Page();
            }

            return NotFound();
        }
    }
}
