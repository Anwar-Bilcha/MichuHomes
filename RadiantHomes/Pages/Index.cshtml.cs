using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.Controllers;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Pages.Houses;

namespace RadiantHomes.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        public List<House> Houses { get; set; }
        public List<House> FeaturedSalesHomes { get; set; }
        public List<House> FeaturedRentHomes { get; set; }
        public List<HousingCompany> RealEstates { get; set; }
        public List<HouseProject> Projects { get; set; }
        public List<PartyAddress> Addresses { get; set; }
        public List<ApartmentHouse> ApartmentHouses { get; set; }

        /// <summary>The apartments shown in the "Apartment" tab (at most 30).</summary>
        public List<ApartmentHouse> FeaturedApartments { get; set; } = new();

        /// <summary>
        /// Cover photo per featured apartment (same photos as ApartmentHouses/Details); missing when it has none,
        /// and the card then shows the brand mark.
        /// </summary>
        public Dictionary<Guid, string> ApartmentPhotoUrls { get; set; } = new();
        public string[] HouseCategories { get; set; }
        public string MissingHousePhoto { get; set; }

        // "Why Choose Michu Homes?" counters — real counts from the database (were hard-coded 120+/5000+/…).
        public int RealEstateCount { get; set; }
        public int ListedHomesCount { get; set; }
        public int ProjectCount { get; set; }
        // Sales/rentals marked Completed. Nothing in the app sets that status yet, so the tile stays hidden while 0.
        public int CompletedDealsCount { get; set; }
        public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;
        }

        public void OnGet()
        {
            RealEstates = _context.HousingCompanies.Include(a => a.Projects).OrderBy(a=>a.Name).ToList();
            Projects = _context.Projects.Include(a => a.Buildings).ToList();
            Addresses = _context.Addresses.ToList();
            Houses = _context.Houses
                .Include(a => a.HouseCategory)
                .Include(a => a.HouseRentInfo)
                .ToList();

            FeaturedSalesHomes = Houses
                .Where(a => a.AvailableFor != null && a.AvailableFor.Equals("Sell", StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();

            FeaturedRentHomes = Houses
                .Where(a => a.AvailableFor != null &&
                            a.AvailableFor.Equals("Rent", StringComparison.OrdinalIgnoreCase) &&
                            (a.HouseRentInfo == null || a.HouseRentInfo.IsOnRent == false))
                .Take(5)
                .ToList();

            // Only listed apartments (approved and available), like the apartment listing page.
            var listedApartments = _context.ApartmentHouses.Where(ApartmentListing.IsListed);
            ApartmentHouses = listedApartments
                .Include(a => a.HouseCategory)
                .ToList();

            FeaturedApartments = ApartmentHouses
                .Where(h => string.Equals(h.HouseCategory?.Name, "Apartment", StringComparison.OrdinalIgnoreCase))
                .Take(30)
                .ToList();
            LoadApartmentPhotos();

            // One tab per category that has listed apartments (no empty tabs for unapproved ones).
            HouseCategories = _context.HouseCategories
                .Where(hc => listedApartments.Any(ah => ah.HouseCategoryId == hc.Id))
                .Select(a => a.Name)
                .ToArray();

            RealEstateCount = RealEstates.Count;
            ListedHomesCount = Houses.Count + ApartmentHouses.Count;
            ProjectCount = Projects.Count;
            CompletedDealsCount =
                _context.HouseSaleTransactions.Count(t => t.Status == "Completed") +
                _context.HouseRentTransactions.Count(t => t.Status == "Completed") +
                _context.ApartmentHouseSaleTransactions.Count(t => t.Status == HouseReserveStatuses.Completed);
        }

        /// <summary>
        /// One query for the featured apartments' company names (they pick the shared photo folder), then one
        /// folder listing per distinct folder (ApartmentCoverPhotos caches them) instead of a disk scan per card.
        /// </summary>
        private void LoadApartmentPhotos()
        {
            if (FeaturedApartments.Count == 0) return;

            var ids = FeaturedApartments.Select(a => a.Id).ToList();
            var companyNames = _context.ApartmentHouses.AsNoTracking()
                .Where(a => ids.Contains(a.Id))
                .Select(a => new { a.Id, Company = a.Building != null && a.Building.Project != null && a.Building.Project.Company != null
                    ? a.Building.Project.Company.Name
                    : null })
                .ToDictionary(a => a.Id, a => a.Company);

            var photos = new ApartmentCoverPhotos(_env);
            foreach (var apartment in FeaturedApartments)
            {
                var url = photos.Find(apartment.Id, companyNames.GetValueOrDefault(apartment.Id), apartment.Bedrooms);
                if (url is not null) ApartmentPhotoUrls[apartment.Id] = url;
            }
        }

    }
}
