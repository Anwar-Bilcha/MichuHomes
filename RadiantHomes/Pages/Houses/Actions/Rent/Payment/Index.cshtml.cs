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

namespace RadiantHomes.Pages.Houses.Actions.Rent.Payment
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

        public IList<HouseRentPayment> HouseRentPayment { get;set; } = default!;

        public async Task OnGetAsync()
        {
            // Michu admins see every payment; everyone else only those of their own rent transactions.
            var customerId = await HouseAccess.CurrentCustomerIdAsync(_userManager, User);
            HouseRentPayment = await _context.HouseRentPayments
                .VisibleTo(TransactionAccess.IsAdmin(User), customerId)
                .Include(h => h.Transaction).ThenInclude(t => t.House)
                .Include(h => h.Transaction).ThenInclude(t => t.Receiver)
                .ToListAsync();
        }
    }
}
