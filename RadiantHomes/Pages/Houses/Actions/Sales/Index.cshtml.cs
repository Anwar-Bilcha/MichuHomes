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

namespace RadiantHomes.Pages.Houses.Actions.Sales
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

        public List<HouseSaleTransaction> HouseSaleTransaction { get;set; } = default!;

        public async Task OnGetAsync()
        {
            // Michu admins see every sale; everyone else only those they are the seller or buyer in.
            // (The house and the parties come with the Includes; the per-row lookups that were here are gone.)
            var customerId = await HouseAccess.CurrentCustomerIdAsync(_userManager, User);
            HouseSaleTransaction = await _context.HouseSaleTransactions
                .VisibleTo(TransactionAccess.IsAdmin(User), customerId)
                .Include(h => h.HomeOwner)
                .Include(h => h.House)
                .Include(h => h.Receiver).ToListAsync();
        }
    }
}
