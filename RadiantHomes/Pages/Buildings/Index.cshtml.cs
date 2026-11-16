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
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Building> Building { get;set; } = default!;

        // A Real Estate Admin without a company: show why the list is empty
        public bool NoCompany { get; set; }

        public async Task OnGetAsync()
        {
            // Visitors see every building (public list); a Real Estate Admin only their own company's.
            var scope = await CompanyScope.ForAsync(User, _context);
            NoCompany = scope.HasNoCompany;
            Building = await scope.Buildings(_context.Buildings)
                .Include(b => b.Project).ToListAsync();
        }
    }
}
