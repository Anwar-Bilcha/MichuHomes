using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses
{
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<House> House { get;set; } = default!;

        public async Task OnGetAsync()
        {
            House = await _context.Houses
                .Include(h => h.HouseAddress)
                .Include(h=>h.HouseRentInfo)
                .Include(h => h.HouseCategory)
                .Include(h => h.HouseHolder)
                .Include(h => h.HouseOwner).ToListAsync();

        }
    }
}
