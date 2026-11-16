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

namespace RadiantHomes.Pages.Houses.Actions.Rent
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
        public HouseRentTransaction HouseRentTransaction { get; set; } = default!;

        /// <summary>Michu admins may change the house and the parties; the owner and the tenant can't.</summary>
        public bool CanEditParties { get; private set; }

        // Shown read-only when the parties can't be changed.
        public string HouseTitle { get; private set; } = string.Empty;
        public string TenantName { get; private set; } = string.Empty;
        public string OwnerName { get; private set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houserenttransaction = await _context.HouseRentTransactions.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (houserenttransaction == null)
            {
                return NotFound();
            }
            // Only a Michu admin or a party to the transaction (owner / tenant).
            if (!await TransactionAccess.CanAccessAsync(_userManager, User, houserenttransaction.OwnerId, houserenttransaction.CustomerId))
            {
                return Forbid();
            }
            HouseRentTransaction = houserenttransaction;
            await LoadPartiesAsync();
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // Check access against the stored transaction, not the posted one.
            var stored = await _context.HouseRentTransactions.AsNoTracking().FirstOrDefaultAsync(m => m.Id == HouseRentTransaction.Id);
            if (stored == null)
            {
                return NotFound();
            }
            if (!await TransactionAccess.CanAccessAsync(_userManager, User, stored.OwnerId, stored.CustomerId))
            {
                return Forbid();
            }

            // Navigation properties are never posted (nullable reference types made them required, so the form
            // never saved). Created/Updated by/on aren't posted either: ApplicationDbContext stamps them.
            HouseFormValidation.IgnoreNavigationProperties(ModelState, nameof(HouseRentTransaction),
                nameof(HouseRentTransaction.House), nameof(HouseRentTransaction.Receiver),
                nameof(HouseRentTransaction.HomeOwner), nameof(HouseRentTransaction.Payment));

            if (TransactionAccess.IsAdmin(User))
            {
                if (!await _context.Houses.AnyAsync(h => h.Id == HouseRentTransaction.HouseId))
                    ModelState.AddModelError("HouseRentTransaction.HouseId", "Please choose the house.");
                if (!await _context.UserProfiles.AnyAsync(c => c.Id == HouseRentTransaction.CustomerId))
                    ModelState.AddModelError("HouseRentTransaction.CustomerId", "Please choose the tenant.");
                if (!await _context.UserProfiles.AnyAsync(c => c.Id == HouseRentTransaction.OwnerId))
                    ModelState.AddModelError("HouseRentTransaction.OwnerId", "Please choose the owner.");
                if (HouseRentTransaction.CustomerId == HouseRentTransaction.OwnerId)
                    ModelState.AddModelError("HouseRentTransaction.CustomerId", "The tenant and the owner must be different people.");
            }
            else
            {
                // The parties can't move the transaction to another house or person: keep the stored ones.
                HouseRentTransaction.HouseId = stored.HouseId;
                HouseRentTransaction.CustomerId = stored.CustomerId;
                HouseRentTransaction.OwnerId = stored.OwnerId;
                foreach (var key in new[] { "HouseId", "CustomerId", "OwnerId" })
                {
                    ModelState.Remove($"{nameof(HouseRentTransaction)}.{key}");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadPartiesAsync();
                return Page();
            }

            _context.Attach(HouseRentTransaction).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HouseRentTransactionExists(HouseRentTransaction.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            TempData["Success"] = "Rent transaction updated.";
            return RedirectToPage("./Index");
        }

        /// <summary>Pickers with readable texts for admins; the current house and names for the parties.</summary>
        private async Task LoadPartiesAsync()
        {
            CanEditParties = TransactionAccess.IsAdmin(User);
            if (CanEditParties)
            {
                ViewData["HouseId"] = HouseSelectLists.Houses(_context, HouseRentTransaction.HouseId);
                ViewData["CustomerId"] = HouseSelectLists.Customers(_context, HouseRentTransaction.CustomerId);
                ViewData["OwnerId"] = HouseSelectLists.Customers(_context, HouseRentTransaction.OwnerId);
                return;
            }
            HouseTitle = await DisplayText.HouseTitleAsync(_context, HouseRentTransaction.HouseId);
            TenantName = await DisplayText.PersonNameAsync(_context, HouseRentTransaction.CustomerId);
            OwnerName = await DisplayText.PersonNameAsync(_context, HouseRentTransaction.OwnerId);
        }

        private bool HouseRentTransactionExists(Guid id)
        {
            return _context.HouseRentTransactions.Any(e => e.Id == id);
        }
    }
}
