using Microsoft.AspNetCore.Mvc;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Pages.ApartmentHouses;
using RadiantHomes.Services.CustomerPortal;
using RadiantHomes.Services.NotificationService;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>One home in the portal: photos, price check, facts, 3D view, comments, similar homes and reserving.</summary>
    public class ApartmentModel : PortalPageModel
    {
        [BindProperty(SupportsGet = true)] public Guid Id { get; set; }

        public ApartmentHouse Apartment { get; private set; } = default!;
        public List<string> Photos { get; private set; } = new();
        public PriceCheck? PriceCheck { get; private set; }
        public List<ApartmentCard> Similar { get; private set; } = new();
        public List<ApartmentHouseReview> Comments { get; private set; } = new();
        public ApartmentHouseSaleTransaction? MyReservation { get; private set; }
        public ApartmentModel3D.Viewer? Model3D { get; private set; }

        /// <summary>Listed (approved and available): it can be reserved.</summary>
        public bool CanBeReserved =>
            Apartment.IsAvailable && string.Equals(Apartment.Status, ApartmentHouseApproval.ApprovedStatus, StringComparison.OrdinalIgnoreCase);

        public double AverageRating => Comments.Count == 0 ? 0 : Comments.Average(c => c.Rating);

        [BindProperty] public string? Notes { get; set; }
        [BindProperty] public int Rating { get; set; }
        [BindProperty] public string? Comment { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            var apartment = await Service<ApartmentCatalog>().FindAsync(Id);
            if (apartment is null) return NotFound();
            Apartment = apartment;

            var catalog = Service<ApartmentCatalog>();
            Photos = catalog.Photos(apartment);
            PriceCheck = await catalog.PriceCheckAsync(apartment);
            Similar = await catalog.SimilarAsync(apartment);
            Comments = await Service<ApartmentComments>().ForApartmentAsync(apartment.Id);
            if (Me.CustomerId is Guid customerId)
                MyReservation = await Service<ReservationService>().ActiveForAsync(customerId, apartment.Id);

            var env = Service<IWebHostEnvironment>();
            if (ApartmentModel3D.Find(env, apartment.Id) is { } model)
                Model3D = new ApartmentModel3D.Viewer(model, apartment.Title, Photos.FirstOrDefault(), ApartmentModel3D.LoadRooms(env, apartment.Id));
            return Page();
        }

        public async Task<IActionResult> OnPostReserveAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (!Me.HasProfile) return CompleteProfileFirst(Url.Page("/Portal/Apartment", new { id = Id }));

            var result = await Service<ReservationService>().ReserveAsync(Id, Me.CustomerId, Me.FullName, Notes);
            if (result.Outcome == ReservationOutcome.NotFound) return NotFound();
            if (!result.Succeeded)
            {
                TempData["Error"] = result.Message;
                return RedirectToPage(new { id = Id });
            }
            TempData["Success"] = "Reservation successful! The real estate company will contact you.";
            return RedirectToPage("/Portal/Reservations");
        }

        public async Task<IActionResult> OnPostCommentAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.CustomerId is not Guid customerId) return CompleteProfileFirst(Url.Page("/Portal/Apartment", new { id = Id }));

            var error = await Service<ApartmentComments>().AddAsync(Id, customerId, Rating, Comment);
            if (error is null) TempData["Success"] = "Thank you for your comment!";
            else TempData["Error"] = error;
            return Redirect(Url.Page("/Portal/Apartment", new { id = Id }) + "#comments");
        }
    }
}
