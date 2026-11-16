using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Utilities;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages
{
    public class CreateModel : PageModel
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CreateModel> _logger;

        public SelectList AddressList { get; set; } = default!;

        [BindProperty]
        public HousingCompany HousingCompany { get; set; } = default!;

        // [BindProperty] keeps the upload POST-only. Without it, IFormFile properties are still bound
        // (IFormFile has an implicit form-file binding source) on every request, including GET, so the
        // implicit "required" check ran on first load and showed "The LogoUrl field is required.".
        [BindProperty]
        [Display(Name = "Company Logo")]
        [Required(ErrorMessage = "Please choose a logo image for the company.")]
        public IFormFile? LogoUrl { get; set; }

        public CreateModel(ApplicationDbContext context, IWebHostEnvironment env, ILogger<CreateModel> logger)
        {
            _env = env;
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // A Real Estate Admin can't register other companies (their own is edited on the Edit page)
            if ((await CompanyScope.ForAsync(User, _context)).IsRestricted) return Forbid();
            await LoadAddressListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if ((await CompanyScope.ForAsync(User, _context)).IsRestricted) return Forbid();
            // HousingCompany.LogoUrl is filled from the upload below, and Address is a navigation
            // property (the form posts AddressId), so neither comes from the form.
            ModelState.Remove("HousingCompany.LogoUrl");
            ModelState.Remove("HousingCompany.Address");

            if (LogoUrl != null && SafeUpload.Validate(LogoUrl, SafeUpload.ImageExtensions) is string logoError)
            {
                ModelState.AddModelError(nameof(LogoUrl), logoError);
            }

            if (!await _context.Addresses.AnyAsync(a => a.AddressID == HousingCompany.AddressId))
            {
                ModelState.Remove("HousingCompany.AddressId");
                ModelState.AddModelError("HousingCompany.AddressId", "Please select an address.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAddressListAsync();
                return Page();
            }

            try
            {
                // Save the logo (same folder/URL scheme as RealEstates/Create) and store its web path
                var safeFolderName = SafeUpload.Segment(
                    HousingCompany.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), "Company");
                var logoDirectory = SafeUpload.Under(Path.Combine(_env.WebRootPath, "uploads", "CompanyLogos"), safeFolderName);
                Directory.CreateDirectory(logoDirectory);

                var fileName = SafeUpload.FileName(LogoUrl!, SafeUpload.ImageExtensions);
                using (var stream = new FileStream(Path.Combine(logoDirectory, fileName), FileMode.Create))
                {
                    await LogoUrl.CopyToAsync(stream);
                }
                HousingCompany.LogoUrl = $"/uploads/CompanyLogos/{safeFolderName}/{fileName}";

                // The EF model declares NEWID() as the key default, but the database column has no default,
                // so EF would insert NULL for an empty Guid key: generate it here (as Projects/Create does).
                // CreatedOn/CreatedBy are stamped by ApplicationDbContext.SaveChanges
                HousingCompany.CompanyId = Guid.NewGuid();

                _context.HousingCompanies.Add(HousingCompany);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created a new Housing Company with ID = {CompanyId}", HousingCompany.CompanyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the housing company.");
                ModelState.AddModelError(string.Empty, "An error occurred while creating the housing company. Please try again.");
                await LoadAddressListAsync();
                return Page();
            }

            // Next onboarding step: the company's financial details (then projects), as in RealEstates/Create
            return RedirectToPage("/Financial/Create", new { companyId = HousingCompany.CompanyId });
        }

        private async Task LoadAddressListAsync()
        {
            var addresses = await _context.Addresses
                .Select(a => new
                {
                    a.AddressID,
                    FormattedAddress = $"{a.City}, {a.SubCity}- Wereda - {a.Woreda}"
                })
                .ToListAsync();

            AddressList = new SelectList(addresses, "AddressID", "FormattedAddress", HousingCompany?.AddressId);
        }
    }
}
