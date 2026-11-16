using System;
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
    /// <summary>
    /// Starts a rent transaction for one house (/Houses/Actions/Rent/Create?id={houseId}).
    /// Without an id the page lets the user pick one of the houses listed for rent.
    /// </summary>
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public CreateModel(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public Guid CustomerId { get; set; }

        [BindProperty]
        public Guid HouseId { get; set; }

        [BindProperty]
        public Guid HouseOwnerId { get; set; }

        [BindProperty]
        public HouseRentTransaction HouseRentTransaction { get; set; } = default!;
        public House? RentedHouse { get; set; }

        /// <summary>True when no house was given: the page shows the house picker.</summary>
        public bool IsChoosingHouse { get; set; }
        public SelectList? RentableHouses { get; set; }

        /// <summary>Set when the signed-in account has no customer profile to rent with.</summary>
        public string? ProfileMissingMessage { get; set; }

        /// <summary>Set when the signed-in user owns the house: owners can't rent their own house.</summary>
        public string? OwnHouseMessage { get; set; }

        private const string OwnHouseText = "This is your own house, so you can't rent it.";

        public async Task<IActionResult> OnGetAsync(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                IsChoosingHouse = true;
                RentableHouses = await LoadRentableHousesAsync(await GetCurrentCustomerIdAsync());
                return Page();
            }

            if (!Guid.TryParse(id, out var parsedHouseId))
            {
                return NotFound();
            }

            RentedHouse = await _context.Houses
                .AsNoTracking()
                .Include(h => h.HouseOwner)
                .Include(h => h.HouseAddress)
                .Include(h => h.HouseRentInfo)
                .FirstOrDefaultAsync(h => h.Id == parsedHouseId);
            if (RentedHouse is null)
            {
                return NotFound();
            }

            HouseId = RentedHouse.Id;
            HouseOwnerId = RentedHouse.HouseOwnerId;

            if (User.Identity?.IsAuthenticated != true)
            {
                return Challenge();
            }

            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId is null)
            {
                ProfileMissingMessage = "Your account isn't linked to a customer profile, so you can't start a rent transaction with it.";
                return Page();
            }

            if (customerId == RentedHouse.HouseOwnerId)
            {
                OwnHouseMessage = OwnHouseText;
                return Page();
            }

            CustomerId = customerId.Value;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            //if (!ModelState.IsValid)
            //{
            //    return Page();
            //}
            var rentedHouse = await _context.Houses
                .Include(h => h.HouseRentInfo)
                .FirstOrDefaultAsync(h => h.Id == HouseId);
            if (rentedHouse is null)
            {
                return NotFound();
            }

            if (User.Identity?.IsAuthenticated != true)
            {
                return Challenge();
            }

            // Don't trust the hidden fields for who rents from whom.
            var customerId = await GetCurrentCustomerIdAsync();
            if (customerId is null)
            {
                return Forbid();
            }
            if (customerId == rentedHouse.HouseOwnerId)
            {
                RentedHouse = await _context.Houses.AsNoTracking()
                    .Include(h => h.HouseAddress)
                    .Include(h => h.HouseRentInfo)
                    .FirstOrDefaultAsync(h => h.Id == rentedHouse.Id);
                OwnHouseMessage = OwnHouseText;
                return Page();
            }

            // The database has no default for the key (despite the NEWID() default in the EF model), so set it here.
            HouseRentTransaction.Id = Guid.NewGuid();
            HouseRentTransaction.HouseId = rentedHouse.Id;
            HouseRentTransaction.OwnerId = rentedHouse.HouseOwnerId;
            HouseRentTransaction.CustomerId = customerId.Value;
            HouseRentTransaction.TransactionDate = DateTime.Now;
            HouseRentTransaction.Status = "Pending";
            AuditFields.ResetForInsert(HouseRentTransaction); // CreatedBy/CreatedOn are stamped by ApplicationDbContext
            if (rentedHouse.HouseRentInfo != null)
            {
                rentedHouse.HouseRentInfo.IsOnRent = true;
            }
            _context.HouseRentTransactions.Add(HouseRentTransaction);

            await _context.SaveChangesAsync();

            return RedirectToPage("/Houses/Actions/Rent/Payment/Create", new { transactionId = HouseRentTransaction.Id });
        }

        /// <summary>
        /// The customer profile of the signed-in account (AppUser.CustomerId). Not the "CustomerId" session value:
        /// that one is only set by the password login, survives a logout and can belong to a previous user.
        /// </summary>
        private Task<Guid?> GetCurrentCustomerIdAsync() => HouseAccess.CurrentCustomerIdAsync(_userManager, User);

        /// <summary>Houses listed for rent that aren't currently rented out.</summary>
        private async Task<SelectList> LoadRentableHousesAsync(Guid? customerId)
        {
            var houses = await _context.Houses.AsNoTracking()
                .Include(h => h.HouseAddress)
                .Include(h => h.HouseRentInfo)
                .Where(h => h.AvailableFor == HouseAvailability.Rent && (h.HouseRentInfo == null || !h.HouseRentInfo.IsOnRent))
                .Where(h => customerId == null || h.HouseOwnerId != customerId) // not the user's own houses
                .OrderBy(h => h.Title)
                .ToListAsync();

            var items = houses.Select(h => new
            {
                h.Id,
                Text = $"{h.Title}" +
                       (h.HouseAddress?.City is { Length: > 0 } city ? $" — {city}" : "") +
                       $" — ETB {HouseAvailability.ListingPrice(h):N0}" +
                       (h.HouseRentInfo is not null ? $" / {HouseAvailability.RentTermLabel(h.HouseRentInfo.RentTerm).ToLowerInvariant()}" : "")
            });
            return new SelectList(items, "Id", "Text");
        }
    }
}
