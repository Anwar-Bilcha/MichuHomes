using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies; // Assuming HouseProject is here
using RadiantHomes.Models; // Assuming your House and Building models are here or similar namespace
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace RadiantHomes.Pages.Projects
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public DetailsModel(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public HouseProject HouseProject { get; set; } = default!;
        public List<string> ProjectPhotoUrls { get; set; } = new List<string>(); // To store photo URLs

        // --- NEW PROPERTIES FOR PROJECT STATISTICS ---
        public int TotalHouses { get; set; }
        public double AverageSalePrice { get; set; }
        public int NumberOfResidentialHouses { get; set; }
        public int NumberOfCommercialHouses { get; set; }
        public int NumberOfHousesSoldRecently { get; set; }
        // ---------------------------------------------

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Eagerly load Company, Buildings, and their associated Houses (including SalePrice, IsSold, SaleDate)
            HouseProject = await _context.Projects
                .Include(hp => hp.Company)
                .Include(hp => hp.Buildings)
                    .ThenInclude(b => b.Houses) // Include the houses within each building
                        .ThenInclude(h => h.Photos) // Include House photos if you need to access them (optional for stats)
                .FirstOrDefaultAsync(m => m.ProjectId == id);

            if (HouseProject == null)
            {
                return NotFound();
            }

            // --- Logic to load project photos ---
            if (HouseProject.Company != null)
            {
                var companyPrefix = HouseProject.Company.Name?.Split(' ').FirstOrDefault() ?? "";
                var projectPhotosFolder = Path.Combine(
                    _webHostEnvironment.WebRootPath,
                    "uploads",
                    "HouseProjects",
                    companyPrefix,
                    HouseProject.ProjectId.ToString()
                );

                if (Directory.Exists(projectPhotosFolder))
                {
                    var imageFiles = Directory.GetFiles(projectPhotosFolder)
                                             .Where(file => file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                             file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                                             file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                             file.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                                             .ToList();

                    foreach (var filePath in imageFiles)
                    {
                        var relativePath = Path.GetRelativePath(_webHostEnvironment.WebRootPath, filePath);
                        ProjectPhotoUrls.Add("/" + relativePath.Replace("\\", "/"));
                    }
                }
            }

            // --- CALCULATE NEW PROJECT STATISTICS ---
            // Ensure Buildings and Houses collections are not null before querying
            var allHousesInProject = HouseProject.Buildings?.SelectMany(b => b.Houses).ToList();

            TotalHouses = allHousesInProject.Count;

            NumberOfResidentialHouses = allHousesInProject.Count(h => h.HouseType == "Residential");
            NumberOfCommercialHouses = allHousesInProject.Count(h => h.HouseType == "Commercial");

            // Calculate Average Sale Price for sold houses
            var soldHousesWithPrice = allHousesInProject
                                        .Where(h => h.IsAvailable ==false)
                                        .ToList(); // Materialize to avoid "could not be translated" errors if complex logic

            AverageSalePrice =soldHousesWithPrice.Any() ?
                               soldHousesWithPrice.Average(h => h.SalePrice) :
                               0; // Default to 0 if no sold houses with price

            // Calculate Houses Sold Recently (e.g., in the last 90 days)
            var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
            NumberOfHousesSoldRecently = allHousesInProject
                                        .Count(h => h.IsAvailable ==false && h.UpdatedOn.HasValue && h.UpdatedOn.Value >= ninetyDaysAgo);
            // ----------------------------------------

            return Page();
        }
    }
}