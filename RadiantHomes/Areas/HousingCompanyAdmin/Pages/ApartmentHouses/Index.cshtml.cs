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

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.ApartmentHouses
{
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<ApartmentHouse> ApartmentHouse { get;set; } = default!;

        // A Real Estate Admin without a company: show why the list is empty
        public bool NoCompany { get; set; }

        public async Task OnGetAsync()
        {
            // A Real Estate Admin only sees their own company's apartments
            var scope = await CompanyScope.ForAsync(User, _context);
            NoCompany = scope.HasNoCompany;
            ApartmentHouse = await scope.Houses(_context.ApartmentHouses)
                .Include(a => a.Building)
                .Include(a => a.HouseAddress)
                .Include(a => a.HouseCategory).ToListAsync();
        }
    }
}
