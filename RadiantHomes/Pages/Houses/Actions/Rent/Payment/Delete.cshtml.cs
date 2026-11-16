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
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public HouseRentPayment HouseRentPayment { get; set; } = default!;

        /// <summary>Why the record could not be deleted (shown above the confirmation).</summary>
        public string? ErrorMessage { get; set; }

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

        public async Task<IActionResult> OnPostAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houserentpayment = await _context.HouseRentPayments
                .Include(p => p.Transaction)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (houserentpayment == null)
            {
                return RedirectToPage("./Index");
            }
            if (!await CanAccessAsync(houserentpayment))
            {
                return Forbid();
            }

            _context.HouseRentPayments.Remove(houserentpayment);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                HouseRentPayment = await _context.HouseRentPayments
                    .AsNoTracking()
                    .Include(p => p.Transaction).ThenInclude(t => t.House)
                    .Include(p => p.Transaction).ThenInclude(t => t.Receiver)
                    .FirstOrDefaultAsync(m => m.Id == id) ?? houserentpayment;
                ErrorMessage = "This rent payment can't be deleted.";
                return Page();
            }

            TempData["Success"] = "Rent payment deleted.";
            return RedirectToPage("./Index");
        }

        /// <summary>A Michu admin or a party to the payment's rent transaction.</summary>
        private Task<bool> CanAccessAsync(HouseRentPayment payment) =>
            payment.Transaction is null
                ? Task.FromResult(TransactionAccess.IsAdmin(User))
                : TransactionAccess.CanAccessAsync(_userManager, User, payment.Transaction.OwnerId, payment.Transaction.CustomerId);
    }
}
