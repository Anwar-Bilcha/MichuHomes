using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Rent.Payment
{
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public HouseRentPayment HouseRentPayment { get; set; } = default!;

        /// <summary>
        /// Michu admins may move the payment to another transaction and change the amounts; the parties of the
        /// transaction may only update the preferred banks.
        /// </summary>
        public bool CanEditParties { get; private set; }

        /// <summary>The payment's transaction ("House — tenant — date"), shown when it can't be changed.</summary>
        public string TransactionText { get; private set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houserentpayment = await _context.HouseRentPayments.AsNoTracking()
                .Include(p => p.Transaction)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (houserentpayment == null)
            {
                return NotFound();
            }
            // Only a Michu admin or a party to the payment's rent transaction.
            if (!await CanAccessAsync(houserentpayment))
            {
                return Forbid();
            }
            HouseRentPayment = houserentpayment;
            LoadSelectLists();
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // Check access against the stored payment, not the posted one.
            var stored = await _context.HouseRentPayments.AsNoTracking()
                .Include(p => p.Transaction)
                .FirstOrDefaultAsync(m => m.Id == HouseRentPayment.Id);
            if (stored == null)
            {
                return NotFound();
            }
            if (!await CanAccessAsync(stored))
            {
                return Forbid();
            }

            // The Transaction navigation is never posted (implicitly required, so the form never saved).
            // Created/Updated by/on aren't posted either: ApplicationDbContext stamps them.
            HouseFormValidation.IgnoreNavigationProperties(ModelState, nameof(HouseRentPayment), nameof(HouseRentPayment.Transaction));

            if (TransactionAccess.IsAdmin(User))
            {
                if (!await _context.HouseRentTransactions.AnyAsync(t => t.Id == HouseRentPayment.TransactionId))
                    ModelState.AddModelError("HouseRentPayment.TransactionId", "Please choose the rent transaction.");
            }
            else
            {
                // Keep the stored transaction and amounts; the parties only choose their banks.
                HouseRentPayment.TransactionId = stored.TransactionId;
                HouseRentPayment.RentCommision = stored.RentCommision;
                HouseRentPayment.RentPricePerTerm = stored.RentPricePerTerm;
                HouseRentPayment.TotalRentPrice = stored.TotalRentPrice;
                foreach (var key in new[] { "TransactionId", "RentCommision", "RentPricePerTerm", "TotalRentPrice" })
                {
                    ModelState.Remove($"{nameof(HouseRentPayment)}.{key}");
                }
            }

            if (!ModelState.IsValid)
            {
                HouseRentPayment.Transaction = stored.Transaction;
                LoadSelectLists();
                return Page();
            }
            HouseRentPayment.Transaction = null!; // don't attach the transaction loaded for the check

            _context.Attach(HouseRentPayment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HouseRentPaymentExists(HouseRentPayment.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            TempData["Success"] = "Rent payment updated.";
            return RedirectToPage("./Index");
        }

        private Task<bool> CanAccessAsync(HouseRentPayment payment) =>
            payment.Transaction is null
                ? Task.FromResult(TransactionAccess.IsAdmin(User))
                : TransactionAccess.CanAccessAsync(_userManager, User, payment.Transaction.OwnerId, payment.Transaction.CustomerId);

        private void LoadSelectLists()
        {
            CanEditParties = TransactionAccess.IsAdmin(User);
            if (CanEditParties)
            {
                ViewData["TransactionId"] = HouseSelectLists.RentTransactions(_context, HouseRentPayment.TransactionId);
                return;
            }
            var transaction = _context.HouseRentTransactions.AsNoTracking()
                .Include(t => t.House)
                .Include(t => t.Receiver)
                .FirstOrDefault(t => t.Id == HouseRentPayment.TransactionId);
            TransactionText = DisplayText.RentTransaction(transaction);
        }

        private bool HouseRentPaymentExists(Guid id)
        {
            return _context.HouseRentPayments.Any(e => e.Id == id);
        }
    }
}
