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

namespace RadiantHomes.Pages.Houses.Actions.Sales
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
        public HouseSaleTransaction HouseSaleTransaction { get; set; } = default!;

        /// <summary>Michu admins may change the house and the parties; the seller and the buyer can't.</summary>
        public bool CanEditParties { get; private set; }

        // Shown read-only when the parties can't be changed.
        public string HouseTitle { get; private set; } = string.Empty;
        public string BuyerName { get; private set; } = string.Empty;
        public string SellerName { get; private set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var housesaletransaction = await _context.HouseSaleTransactions.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (housesaletransaction == null)
            {
                return NotFound();
            }
            // Only a Michu admin or a party to the sale (seller / buyer).
            if (!await TransactionAccess.CanAccessAsync(_userManager, User, housesaletransaction.OwnerId, housesaletransaction.CustomerId))
            {
                return Forbid();
            }
            HouseSaleTransaction = housesaletransaction;
            await LoadPartiesAsync();
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // Check access against the stored sale, not the posted one.
            var stored = await _context.HouseSaleTransactions.AsNoTracking().FirstOrDefaultAsync(m => m.Id == HouseSaleTransaction.Id);
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
            HouseFormValidation.IgnoreNavigationProperties(ModelState, nameof(HouseSaleTransaction),
                nameof(HouseSaleTransaction.House), nameof(HouseSaleTransaction.Receiver),
                nameof(HouseSaleTransaction.HomeOwner), nameof(HouseSaleTransaction.Payment));

            if (TransactionAccess.IsAdmin(User))
            {
                if (!await _context.Houses.AnyAsync(h => h.Id == HouseSaleTransaction.HouseId))
                    ModelState.AddModelError("HouseSaleTransaction.HouseId", "Please choose the house.");
                if (!await _context.UserProfiles.AnyAsync(c => c.Id == HouseSaleTransaction.CustomerId))
                    ModelState.AddModelError("HouseSaleTransaction.CustomerId", "Please choose the buyer.");
                if (!await _context.UserProfiles.AnyAsync(c => c.Id == HouseSaleTransaction.OwnerId))
                    ModelState.AddModelError("HouseSaleTransaction.OwnerId", "Please choose the seller.");
                if (HouseSaleTransaction.CustomerId == HouseSaleTransaction.OwnerId)
                    ModelState.AddModelError("HouseSaleTransaction.CustomerId", "The buyer and the seller must be different people.");
            }
            else
            {
                // The parties can't move the sale to another house or person: keep the stored ones.
                HouseSaleTransaction.HouseId = stored.HouseId;
                HouseSaleTransaction.CustomerId = stored.CustomerId;
                HouseSaleTransaction.OwnerId = stored.OwnerId;
                foreach (var key in new[] { "HouseId", "CustomerId", "OwnerId" })
                {
                    ModelState.Remove($"{nameof(HouseSaleTransaction)}.{key}");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadPartiesAsync();
                return Page();
            }

            _context.Attach(HouseSaleTransaction).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HouseSaleTransactionExists(HouseSaleTransaction.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            TempData["Success"] = "House sale updated.";
            return RedirectToPage("./Index");
        }

        /// <summary>Pickers with readable texts for admins; the current house and names for the parties.</summary>
        private async Task LoadPartiesAsync()
        {
            CanEditParties = TransactionAccess.IsAdmin(User);
            if (CanEditParties)
            {
                ViewData["HouseId"] = HouseSelectLists.Houses(_context, HouseSaleTransaction.HouseId);
                ViewData["CustomerId"] = HouseSelectLists.Customers(_context, HouseSaleTransaction.CustomerId);
                ViewData["OwnerId"] = HouseSelectLists.Customers(_context, HouseSaleTransaction.OwnerId);
                return;
            }
            HouseTitle = await DisplayText.HouseTitleAsync(_context, HouseSaleTransaction.HouseId);
            BuyerName = await DisplayText.PersonNameAsync(_context, HouseSaleTransaction.CustomerId);
            SellerName = await DisplayText.PersonNameAsync(_context, HouseSaleTransaction.OwnerId);
        }

        private bool HouseSaleTransactionExists(Guid id)
        {
            return _context.HouseSaleTransactions.Any(e => e.Id == id);
        }
    }
}
