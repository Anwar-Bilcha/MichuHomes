using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

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

        public async Task OnGetAsync()
        {
            ApartmentHouse = await _context.ApartmentHouses
                .Include(a => a.Building)
                .Include(a => a.HouseAddress)
                .Include(a => a.HouseCategory).ToListAsync();
        }
    }
}
