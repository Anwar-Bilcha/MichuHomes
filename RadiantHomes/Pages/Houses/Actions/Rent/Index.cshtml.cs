using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<AppUser> _userManager;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IList<HouseRentTransaction> HouseRentTransaction { get;set; } = default!;

        public async Task OnGetAsync()
        {
            // Michu admins see every transaction; everyone else only those they are the owner or tenant of.
            var customerId = await HouseAccess.CurrentCustomerIdAsync(_userManager, User);
            HouseRentTransaction = await _context.HouseRentTransactions
                .VisibleTo(TransactionAccess.IsAdmin(User), customerId)
                .Include(h => h.HomeOwner)
                .Include(h => h.House)
                .Include(h => h.Receiver).ToListAsync();
        }
    }
}
