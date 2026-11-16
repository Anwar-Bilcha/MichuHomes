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
    /// <summary>
    /// Registers a house sale (/Houses/Actions/Sales/Create?id={houseId}, the "Buy House" button).
    /// Michu admins pick the house, buyer and seller; everyone else buys as themselves (their customer profile)
    /// from the house's owner, and the sale starts as "Pending".
    /// </summary>
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public HouseSaleTransaction HouseSaleTransaction { get; set; } = default!;

        /// <summary>Michu admins pick the house and both parties, the date and the status.</summary>
        public bool CanEditParties { get; private set; }

        /// <summary>No house was given: a buyer picks one of the houses listed for sale.</summary>
        public bool IsChoosingHouse { get; private set; }

        /// <summary>Set when the signed-in account has no customer profile to buy with.</summary>
        public string? ProfileMissingMessage { get; private set; }

        // Shown read-only to buyers.
        public string HouseTitle { get; private set; } = string.Empty;
        public string BuyerName { get; private set; } = string.Empty;
        public string SellerName { get; private set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            HouseSaleTransaction = new HouseSaleTransaction { TransactionDate = DateTime.Now, Status = "Pending" };
            if (id is Guid houseId)
            {
                var house = await _context.Houses.AsNoTracking().FirstOrDefaultAsync(h => h.Id == houseId);
                if (house is null)
                {
                    return NotFound();
                }
                HouseSaleTransaction.HouseId = house.Id;
                HouseSaleTransaction.OwnerId = house.HouseOwnerId;
            }

            if (!TransactionAccess.IsAdmin(User))
            {
                var customerId = await HouseAccess.CurrentCustomerIdAsync(_userManager, User);
                if (customerId is null)
                {
                    ProfileMissingMessage = "Your account isn't linked to a customer profile, so you can't buy a house with it.";
                    return Page();
                }
                HouseSaleTransaction.CustomerId = customerId.Value;
            }

            await LoadFormDataAsync();
            return Page();
        }

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // Navigation properties are never posted (nullable reference types made them required, so the form
            // never saved).
            HouseFormValidation.IgnoreNavigationProperties(ModelState, nameof(HouseSaleTransaction),
                nameof(HouseSaleTransaction.House), nameof(HouseSaleTransaction.Receiver),
                nameof(HouseSaleTransaction.HomeOwner), nameof(HouseSaleTransaction.Payment));
            void SetError(string field, string message)
            {
                var key = $"{nameof(HouseSaleTransaction)}.{field}";
                ModelState.Remove(key); // replaces the generic "The value '' is invalid."
                ModelState.AddModelError(key, message);
            }

            var house = await _context.Houses.AsNoTracking().FirstOrDefaultAsync(h => h.Id == HouseSaleTransaction.HouseId);
            if (TransactionAccess.IsAdmin(User))
            {
                if (house is null) SetError("HouseId", "Please choose the house.");
                if (!await _context.UserProfiles.AnyAsync(c => c.Id == HouseSaleTransaction.CustomerId)) SetError("CustomerId", "Please choose the buyer.");
                if (!await _context.UserProfiles.AnyAsync(c => c.Id == HouseSaleTransaction.OwnerId)) SetError("OwnerId", "Please choose the seller.");
                if (HouseSaleTransaction.CustomerId != Guid.Empty && HouseSaleTransaction.CustomerId == HouseSaleTransaction.OwnerId)
                    SetError("CustomerId", "The buyer and the seller must be different people.");
            }
            else
            {
                // Buyers buy as themselves, from the house's owner; never trust the posted parties, date or status.
                var customerId = await HouseAccess.CurrentCustomerIdAsync(_userManager, User);
                if (customerId is null)
                {
                    return Forbid();
                }
                foreach (var key in new[] { "CustomerId", "OwnerId", "TransactionDate", "Status" })
                {
                    ModelState.Remove($"{nameof(HouseSaleTransaction)}.{key}");
                }
                HouseSaleTransaction.CustomerId = customerId.Value;
                HouseSaleTransaction.TransactionDate = DateTime.Now;
                HouseSaleTransaction.Status = "Pending";
                if (house is null || !HouseAvailability.IsForSale(house.AvailableFor))
                {
                    SetError("HouseId", "Please choose a house that is listed for sale.");
                }
                else
                {
                    HouseSaleTransaction.OwnerId = house.HouseOwnerId;
                    if (house.HouseOwnerId == customerId.Value)
                    {
                        SetError("HouseId", "This is your own house, so you can't buy it.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                IsChoosingHouse = !TransactionAccess.IsAdmin(User) && !string.IsNullOrEmpty(Request.Form["ChoosingHouse"]);
                await LoadFormDataAsync();
                return Page();
            }

            AuditFields.ResetForInsert(HouseSaleTransaction); // stamped by ApplicationDbContext on save
            _context.HouseSaleTransactions.Add(HouseSaleTransaction);
            await _context.SaveChangesAsync();

            TempData["Success"] = "House sale registered.";
            return RedirectToPage("./Index");
        }

        /// <summary>Admins: pickers with readable texts. Buyers: the house (or a picker of houses for sale) and the names.</summary>
        private async Task LoadFormDataAsync()
        {
            CanEditParties = TransactionAccess.IsAdmin(User);
            if (CanEditParties)
            {
                ViewData["HouseId"] = HouseSelectLists.Houses(_context, HouseSaleTransaction.HouseId);
                ViewData["CustomerId"] = HouseSelectLists.Customers(_context, HouseSaleTransaction.CustomerId);
                ViewData["OwnerId"] = HouseSelectLists.Customers(_context, HouseSaleTransaction.OwnerId);
                return;
            }

            BuyerName = await DisplayText.PersonNameAsync(_context, HouseSaleTransaction.CustomerId);
            if (HouseSaleTransaction.HouseId == Guid.Empty || IsChoosingHouse)
            {
                IsChoosingHouse = true;
                ViewData["HouseId"] = HouseSelectLists.Houses(_context, HouseSaleTransaction.HouseId, forSaleOnly: true);
                return;
            }
            HouseTitle = await DisplayText.HouseTitleAsync(_context, HouseSaleTransaction.HouseId);
            SellerName = await DisplayText.PersonNameAsync(_context, HouseSaleTransaction.OwnerId);
        }
    }
}
