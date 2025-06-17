using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Rent
{
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<HouseRentTransaction> HouseRentTransaction { get;set; } = default!;

        public async Task OnGetAsync()
        {
            HouseRentTransaction = await _context.HouseRentTransactions
                .Include(h => h.HomeOwner)
                .Include(h => h.House)
                .Include(h => h.Receiver).ToListAsync();
        }
    }
}
