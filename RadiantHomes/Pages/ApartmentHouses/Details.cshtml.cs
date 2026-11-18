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
using RadiantHomes.Services.CustomerPortal;
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

        /// <summary>Cover photo per similar house (ApartmentCoverPhotos); missing → brand mark.</summary>
        public Dictionary<Guid, string> SimilarPhotoUrls { get; } = new();

        /// <summary>The "3D live view" card, when the apartment has a 3D model (see ApartmentModel3D).</summary>
        public ApartmentModel3D.Viewer? Model3D { get; set; }

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
            if (!await RadiantHomes.Services.Properties.ApartmentVisibility.CanViewAsync(_context, await RadiantHomes.Services.Security.AccessExtensions.AccessAsync(HttpContext), apartmenthouse, user?.CustomerId))
            {
                return NotFound(); // not listed (waiting for approval, hidden, sold, or its company isn't approved)
            }

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

            // Load Photos: this apartment's own uploads first (saved by ApartmentHouses/Create),
            // then the shared photos for the company's apartments with the same number of bedrooms.
            Configurations.Initialize(_env);
            var ownPhotosFolder = Path.Combine(_env.WebRootPath, "uploads", "apartmentphotos", ApartmentHouse.Id.ToString());
            var photosFolder = Path.Combine(Configurations.ApartmentHousePhotosFolder, CompanyName, $"{ApartmentHouse.Bedrooms}bed");
            foreach (var folder in new[] { ownPhotosFolder, photosFolder })
            {
                if (!Directory.Exists(folder)) continue;
                Photos.AddRange(Directory.GetFiles(folder)
                    .OrderBy(f => System.IO.File.GetCreationTimeUtc(f))
                    .Select(f => "/" + Path.GetRelativePath(_env.WebRootPath, f).Replace("\\", "/")));
            }
            if (ApartmentModel3D.Find(_env, ApartmentHouse.Id) is { } model3D)
            {
                Model3D = new ApartmentModel3D.Viewer(model3D, ApartmentHouse.Title, Photos.FirstOrDefault(),
                                                      ApartmentModel3D.LoadRooms(_env, ApartmentHouse.Id));
            }
            // Fetch Similar Houses (e.g., same bedrooms and nearby price range ±20%)
            double minPrice = ApartmentHouse.SalePrice * 0.8;
            double maxPrice = ApartmentHouse.SalePrice * 1.2;

            SimilarHouses = await _context.ApartmentHouses
                .Where(RadiantHomes.Pages.Houses.ApartmentListing.IsListed) // only listed (approved, available) apartments
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

            var coverPhotos = new RadiantHomes.Pages.Houses.ApartmentCoverPhotos(_env);
            foreach (var similar in SimilarHouses)
            {
                var url = coverPhotos.Find(similar.Id, similar.Building?.Project?.Company?.Name, similar.Bedrooms);
                if (url is not null) SimilarPhotoUrls[similar.Id] = url;
            }

            return Page();
        }
        [BindProperty]
        public string CustomerName { get; set; }
        [BindProperty]
        public ApartmentHouseSaleTransaction Transaction { get; set; } = new();

        // Comments and reservations go through the same services as the customer portal (Services/CustomerPortal).
        // Results are reported through TempData: ModelState does not survive the redirect back to this page.
        public async Task<IActionResult> OnPostSubmitReviewAsync(Guid itemid, [FromServices] ApartmentComments comments)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "Please sign in to leave a review.";
                return RedirectToPage(new { id = itemid });
            }
            if (user.CustomerId is not Guid reviewerId || reviewerId == Guid.Empty)
            {
                TempData["Error"] = "Could not find your customer profile.";
                return RedirectToPage(new { id = itemid });
            }

            var error = await comments.AddAsync(itemid, reviewerId, HouseReviews?.Rating ?? 0, HouseReviews?.Comment);
            if (error is null) TempData["Success"] = "Thank you for your review!";
            else TempData["Error"] = error;
            return RedirectToPage(new { id = itemid });
        }

        public async Task<IActionResult> OnPostReserveHouseAsync(Guid itemid, [FromServices] ReservationService reservations)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "Please sign in to reserve this house.";
                return RedirectToPage(new { id = itemid });
            }

            var result = await reservations.ReserveAsync(itemid, user.CustomerId, CustomerName, Transaction?.Notes);
            if (result.Outcome == ReservationOutcome.NotFound) return NotFound();
            if (result.Succeeded) TempData["Success"] = result.Message;
            else TempData["Error"] = result.Message;
            return RedirectToPage(new { id = itemid });
        }

    }
}
