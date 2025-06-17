using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Shared;
using RadiantHomes.Services.NotificationService;

namespace RadiantHomes.Pages.ApartmentHouses
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<AppUser> _userManager;
        private readonly INotificationService _notification;

        public DetailsModel(ApplicationDbContext context, IWebHostEnvironment env, UserManager<AppUser> userManager, INotificationService notification)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
            _notification = notification;
        }
        [BindProperty]
        public ApartmentHouseReview HouseReviews { get; set; } = default!;
        public List<ApartmentHouseReview> Reviews { get; set; } = default!;

        public ApartmentHouse ApartmentHouse { get; set; } = default!;
        public string CompanyName { get; set; } = default!;
        public List<string> Photos { get; set; } = new();
        public List<ApartmentHouse> SimilarHouses { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var apartmenthouse = await _context.ApartmentHouses
                .Include(a => a.Building)
                    .ThenInclude(b => b.Project)
                        .ThenInclude(p => p.Company)
                .Include(a => a.HouseAddress)
              .Include(a=>a.Reviews).ThenInclude(a=>a.ReviewerCustomer)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (apartmenthouse is null)
            {
                return NotFound();
            }
            var user = await _userManager.GetUserAsync(User);

            if (user?.CustomerId != null)
            {
                Transaction.ApartmentHouseId = apartmenthouse.Id;
                Transaction.CustomerId = user.CustomerId.Value;
                Transaction.OwnerId = apartmenthouse.Building.Project.Company.CompanyId;
            }

            Reviews = apartmenthouse.Reviews.OrderByDescending(r => r.ReviewDate).ToList();
            HouseReviews = new ApartmentHouseReview();
            ApartmentHouse = apartmenthouse;
            CompanyName = ApartmentHouse.Building.Project.Company.Name.Split(" ")[0] ?? "default";

            // Load Photos
            Configurations.Initialize(_env);
            var photosFolder = Path.Combine(Configurations.ApartmentHousePhotosFolder, CompanyName, $"{ApartmentHouse.Bedrooms}bed");
            if (Directory.Exists(photosFolder))
            {
                var files = Directory.GetFiles(photosFolder);
                Photos = files.Select(f =>
                {
                    var relativePath = f.Substring(f.IndexOf("wwwroot") + "wwwroot".Length).Replace("\\", "/");
                    return relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
                }).ToList();
            }
            // Fetch Similar Houses (e.g., same bedrooms and nearby price range ±20%)
            double minPrice = ApartmentHouse.SalePrice * 0.8;
            double maxPrice = ApartmentHouse.SalePrice * 1.2;

            SimilarHouses = await _context.ApartmentHouses
                .Include(a => a.HouseAddress)
                .Include(a=>a.Building)
                .ThenInclude(a=>a.Project)
                .ThenInclude(a=>a.Company)
                .Where(a =>
                    a.Id != ApartmentHouse.Id &&
                    a.Bedrooms == ApartmentHouse.Bedrooms &&
                    a.SalePrice >= minPrice &&
                    a.SalePrice <= maxPrice &&
                    a.AvailableFor == ApartmentHouse.AvailableFor)
                .OrderBy(a => a.SalePrice)
                .Take(12)
                .ToListAsync();

            return Page();
        }
        [BindProperty]
        public string CustomerName { get; set; }
        [BindProperty]
        public ApartmentHouseSaleTransaction Transaction { get; set; } = new();

        public async Task<IActionResult> OnPostSubmitReviewAsync(Guid itemid)
        {
            var apartmentHouse = await _context.ApartmentHouses.FindAsync(itemid);
            if (apartmentHouse == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.CustomerId == Guid.Empty)
            {
                TempData["Error"] = "Could not find your customer profile.";
                return RedirectToPage(new { id = itemid });
            }
            var review = new ApartmentHouseReview
            {
                Id = Guid.NewGuid(),
                HouseId = itemid,
                ReviewerCustomerId = user.CustomerId.Value,
                Rating = HouseReviews.Rating,
                Comment = HouseReviews.Comment,
                ReviewDate = DateTime.UtcNow
            };

            _context.ApartmentHouseReviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Thank you for your review!";
            return RedirectToPage(new { id = itemid });
        }
        public async Task<IActionResult> OnPostReserveHouseAsync(Guid itemid)
        {
            var house = _context.ApartmentHouses.Include(a=>a.Building).ThenInclude(a=>a.Project).ThenInclude(a=>a.Company).ThenInclude(a=>a.ContactPerson).FirstOrDefault(a=>a.Id==itemid);
            if (house == null) return NotFound();
            var user = await _userManager.GetUserAsync(User);
            var ownerId = house.Building.Project.Company.ContactPerson.FirstOrDefault().Id;

            if (user == null || user.CustomerId == Guid.Empty)
            {
                ModelState.AddModelError(string.Empty,"Could not find your customer profile.");
                return RedirectToPage(new { id = itemid });
            }

            if(user.CustomerId.HasValue && ownerId!=Guid.Empty)
            {
            house.Status = "Reserved by Customer";
            house.IsAvailable = false;
            Transaction.ApartmentHouseId = house.Id;
                Transaction.OwnerId = ownerId;
            Transaction.CustomerId = user.CustomerId.Value;
            Transaction.TransactionDate = DateTime.Now;
            Transaction.CreatedOn = DateTime.Now;
            Transaction.CreatedBy = Transaction.CustomerId.ToString();
            Transaction.UpdatedBy = Transaction.CustomerId.ToString();
            }
            _context.ApartmentHouseSaleTransactions.Add(Transaction);
            Notification notification = new Notification
            {
                Datatype = EntityAffecteds.ApartmentHouses.ToString(),
                ReceiverAdminId = ownerId,
                ReceiverCompanyId = house.Building.Project.Company.CompanyId,
                RecipientRole = MichuHomesRoles.MichuAdmin,
                CreatedAt = DateTime.Now,
                DataKey=house.Id.ToString(),
                Message = $"House reservation request for {house.Id}",
                Title = $"{CustomerName} reserved a house",
                RedirectUrl = $"/ApartmentHouses/details?id={house.Id}"
            };
            try
            {
                await _notification.CreateNotificationAsync(notification);
                await _context.SaveChangesAsync();
                
                TempData["Success"] = "Reservation successful!";
            }
            catch (Exception e)
            {
                ModelState.AddModelError("ReservationError", e.Message);
            }

            return RedirectToPage(new { id = Transaction.ApartmentHouseId });
        }

    }
}
