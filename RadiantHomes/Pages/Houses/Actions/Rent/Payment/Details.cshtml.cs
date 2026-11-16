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
    public class DetailsModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public HouseRentPayment HouseRentPayment { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houserentpayment = await _context.HouseRentPayments
                .AsNoTracking()
                .Include(p => p.Transaction).ThenInclude(t => t.House)
                .Include(p => p.Transaction).ThenInclude(t => t.Receiver)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (houserentpayment is null)
            {
                return NotFound();
            }
            if (!await CanAccessAsync(houserentpayment))
            {
                return Forbid();
            }

            HouseRentPayment = houserentpayment;
            return Page();
        }

        /// <summary>A Michu admin or a party to the payment's rent transaction.</summary>
        private Task<bool> CanAccessAsync(HouseRentPayment payment) =>
            payment.Transaction is null
                ? Task.FromResult(TransactionAccess.IsAdmin(User))
                : TransactionAccess.CanAccessAsync(_userManager, User, payment.Transaction.OwnerId, payment.Transaction.CustomerId);
    }
}
