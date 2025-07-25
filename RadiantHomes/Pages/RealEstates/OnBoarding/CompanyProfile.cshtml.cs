using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using System.Security.Claims;

namespace RadiantHomes.Pages.RealEstates.OnBoarding.Profile
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<CreateModel> _logger;

        [BindProperty]
        public HousingCompany HousingCompany { get; set; } = default!;

        [BindProperty]
        public IFormFile? Logo { get; set; }

        public CreateModel(ApplicationDbContext context, IWebHostEnvironment env, ILogger<CreateModel> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        public IActionResult OnGet()
        {

            ViewData["AddressId"] = new SelectList(_context.Addresses, "AddressID", "FormattedAddressCompact", HousingCompany?.AddressId);
            return Page();

        }

        public async Task<IActionResult> OnPostAsync()
        {
            //if (!ModelState.IsValid)
            //{
            //    ViewData["AddressId"] = new SelectList(_context.Addresses, "AddressID", "FormattedAddress", HousingCompany.AddressId);
            //    return Page();
            //}

            // Duplicate Check
            bool isDuplicate = await _context.HousingCompanies
                .AnyAsync(h => h.Name == HousingCompany.Name && h.LicenseNumber == HousingCompany.LicenseNumber);

            if (isDuplicate)
            {
                ModelState.AddModelError(string.Empty, "A housing company with the same name and license number already exists.");
                ViewData["AddressId"] = new SelectList(_context.Addresses, "AddressID", "FormattedAddress", HousingCompany.AddressId);
                return Page();
            }

            // File Upload Handling
            if (Logo != null)
            {
                var folderName = HousingCompany.Name.Split(" ")[0];
                var safeFolderName = Path.GetInvalidFileNameChars()
                                         .Aggregate(folderName, (current, c) => current.Replace(c.ToString(), ""));

                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "CompanyLogos", safeFolderName);

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(Logo.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await Logo.CopyToAsync(stream);
                }

                // Save web-accessible URL (not physical path)
                HousingCompany.LogoUrl = $"/uploads/CompanyLogos/{safeFolderName}/{fileName}";
            }

            // Audit Info
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";
            var currentTime = DateTime.Now;

            HousingCompany.CreatedBy = currentUser;
            HousingCompany.CreatedOn = currentTime;
            HousingCompany.UpdatedBy = currentUser;
            HousingCompany.UpdatedOn = currentTime;

            _context.HousingCompanies.Add(HousingCompany);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Housing company called {HousingCompany.Name} was successfully created!";
            return RedirectToPage("./FinancialDetails", new { companyId = HousingCompany.CompanyId });
        }
    }
}
