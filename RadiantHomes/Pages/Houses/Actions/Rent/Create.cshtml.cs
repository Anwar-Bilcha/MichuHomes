using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Rent
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Guid CustomerId { get; set; }

        [BindProperty]
        public Guid HouseId { get; set; }

        [BindProperty]
        public Guid HouseOwnerId { get; set; }

        [BindProperty]
        public HouseRentTransaction HouseRentTransaction { get; set; } = default!;
        public House RentedHouse { get; set; }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (!Guid.TryParse(id, out var parsedHouseId))
            {
                return NotFound();
            }

            RentedHouse = await _context.Houses
                .Include(h => h.HouseOwner)
                .FirstOrDefaultAsync(h => h.Id == parsedHouseId)??new();
            RentedHouse.HouseRentInfo = new RentInfo();
            if (RentedHouse is null)
            {
                return NotFound();
            }

            HouseId = RentedHouse.Id;
            HouseOwnerId = RentedHouse.HouseOwnerId;

            var customerIdStr = HttpContext.Session.GetString("CustomerId");
            if (string.IsNullOrEmpty(customerIdStr) || !Guid.TryParse(customerIdStr, out var parsedCustomerId))
            {
                return RedirectToPage("/Identity/Account/Login"); // or show an error
            }

            CustomerId = parsedCustomerId;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            //if (!ModelState.IsValid)
            //{
            //    return Page();
            //}
            var user = _context.UserProfiles.FirstOrDefault(a => a.Id == CustomerId);
            var RentedHouse = _context.Houses.Find(HouseId)??new();
            HouseRentTransaction.HouseId = HouseId;
            HouseRentTransaction.OwnerId = HouseOwnerId;
            HouseRentTransaction.CustomerId = CustomerId;
            HouseRentTransaction.TransactionDate = DateTime.Now;
            HouseRentTransaction.Status = "Pending";
            HouseRentTransaction.CreatedBy = user?.FirstName + " "+user?.LastName;
            HouseRentTransaction.CreatedOn = DateTime.UtcNow;
            if(RentedHouse.HouseRentInfo !=null)
            {
                RentedHouse.HouseRentInfo.IsOnRent = true;

            }
            _context.HouseRentTransactions.Add(HouseRentTransaction);

            await _context.SaveChangesAsync();

            return RedirectToPage("/Houses/Actions/Rent/Payment/Create", new { transactionId = HouseRentTransaction.Id });
        }
    }
}
