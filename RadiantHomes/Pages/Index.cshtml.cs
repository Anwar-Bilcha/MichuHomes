using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.Controllers;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApplicationDbContext _context;
        public List<House> Houses { get; set; }
        public List<House> FeaturedSalesHomes { get; set; }
        public List<House> FeaturedRentHomes { get; set; }
        public List<HousingCompany> RealEstates { get; set; }
        public List<HouseProject> Projects { get; set; }
        public List<PartyAddress> Addresses { get; set; }
        public List<ApartmentHouse> ApartmentHouses { get; set; }
        public string[] HouseCategories { get; set; }
        public string MissingHousePhoto { get; set; }
        public IndexModel(ILogger<IndexModel> logger,ApplicationDbContext context)
        {
            _context = context;
            _logger = logger;
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

            ApartmentHouses = _context.ApartmentHouses
                .Include(a => a.HouseCategory)
                .ToList();

            HouseCategories = _context.HouseCategories
    .Where(hc => _context.ApartmentHouses.Any(ah => ah.HouseCategoryId == hc.Id)).Select(a=>a.Name)
    .ToArray();
        }

    }
}
