using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Utilities;

namespace RadiantHomes.Pages.RealEstates
{
    /// <summary>
    /// What the housing-company forms share (RealEstates/Create, RealEstates/Edit, OnBoarding/CompanyProfile):
    /// server-side validation, the status options, the address dropdown and the logo upload.
    /// </summary>
    public static class CompanyForm
    {
        /// <summary>
        /// Company statuses the app works with: "Pending" is the model default for new companies, "Active" is what the
        /// company list highlights as active, and the HousingCompanyAdmin create form offers Active / Inactive / Pending.
        /// </summary>
        public static readonly string[] Statuses = { "Pending", "Active", "Inactive" };

        /// <summary>Status options; a saved value outside the list is kept as an extra option so an edit doesn't change it.</summary>
        public static IEnumerable<SelectListItem> StatusItems(string? current)
        {
            var items = Statuses.Select(s => new SelectListItem(s, s)).ToList();
            if (!string.IsNullOrWhiteSpace(current) && !Statuses.Contains(current))
                items.Add(new SelectListItem($"{current} (current value)", current));
            return items;
        }

        public static async Task<SelectList> AddressSelectListAsync(ApplicationDbContext context, Guid? selected)
        {
            var addresses = (await context.Addresses.AsNoTracking().ToListAsync())
                .Select(a => new { a.AddressID, Text = a.FormattedAddressCompact })
                .OrderBy(a => a.Text)
                .ToList();
            return new SelectList(addresses, "AddressID", "Text", selected == Guid.Empty ? null : selected);
        }

        /// <summary>
        /// Validates a posted company. Navigation properties are never posted, so their (implicit) required errors are
        /// removed; the address, e-mail, dates and status get real checks. <paramref name="savedStatus"/> is the
        /// company's current status on Edit, which stays acceptable even if it isn't a standard one.
        /// </summary>
        public static async Task ValidateAsync(ApplicationDbContext context, ModelStateDictionary modelState, HousingCompany company,
                                               string? savedStatus = null)
        {
            const string p = nameof(HousingCompany) + ".";
            foreach (var key in new[] { nameof(HousingCompany.Address), nameof(HousingCompany.ContactPerson), nameof(HousingCompany.FinancialDetails),
                                        nameof(HousingCompany.Projects), nameof(HousingCompany.CustomerTestimonials), nameof(HousingCompany.Notifications),
                                        // Optional text (stored as empty strings) and the logo, which is uploaded separately.
                                        nameof(HousingCompany.Website), nameof(HousingCompany.LogoUrl) })
            {
                modelState.Remove(p + key);
            }
            company.Website = company.Website?.Trim() ?? string.Empty;
            company.LogoUrl = company.LogoUrl?.Trim() ?? string.Empty;

            // An empty address choice is a binding error ("The value '' is invalid."); replaced by an existence check.
            modelState.Remove(p + nameof(HousingCompany.AddressId));
            if (company.AddressId == Guid.Empty || !await context.Addresses.AnyAsync(a => a.AddressID == company.AddressId))
                modelState.AddModelError(p + nameof(HousingCompany.AddressId), "Please select the company's address.");

            var dateKey = p + nameof(HousingCompany.EstablishedDate);
            if (modelState.TryGetValue(dateKey, out var dateEntry) && dateEntry.Errors.Count > 0)
            {
                dateEntry.Errors.Clear();
                modelState.AddModelError(dateKey, "Please enter the date the company was established.");
            }
            else if (company.EstablishedDate == default)
                modelState.AddModelError(dateKey, "Please enter the date the company was established.");
            else if (company.EstablishedDate.Date > DateTime.Today)
                modelState.AddModelError(dateKey, "The established date can't be in the future.");

            if (!string.IsNullOrWhiteSpace(company.Email) && !new EmailAddressAttribute().IsValid(company.Email))
                modelState.AddModelError(p + nameof(HousingCompany.Email), "Please enter a valid e-mail address.");

            if (company.Website.Length > 0 && !Uri.TryCreate(company.Website.Contains("://") ? company.Website : "https://" + company.Website, UriKind.Absolute, out _))
                modelState.AddModelError(p + nameof(HousingCompany.Website), "Please enter a valid website address.");

            var statusOk = Statuses.Contains(company.Status) || (savedStatus != null && company.Status == savedStatus);
            if (!statusOk)
            {
                modelState.Remove(p + nameof(HousingCompany.Status));
                modelState.AddModelError(p + nameof(HousingCompany.Status), "Please select a status.");
            }
        }

        /// <summary>Validates the optional logo upload; returns false (with a model error) when it can't be accepted.</summary>
        public static bool ValidateLogo(ModelStateDictionary modelState, IFormFile? logo, string key = "Logo")
        {
            if (logo == null || logo.Length == 0) return true;
            var error = SafeUpload.Validate(logo, SafeUpload.ImageExtensions);
            if (error == null) return true;
            modelState.AddModelError(key, error);
            return false;
        }

        /// <summary>Saves the logo under wwwroot/uploads/CompanyLogos/{first word of the name}/ and returns its URL.</summary>
        public static async Task<string> SaveLogoAsync(IWebHostEnvironment env, string companyName, IFormFile logo)
        {
            var folderName = SafeUpload.Segment(companyName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), "company");
            var folder = SafeUpload.Under(Path.Combine(env.WebRootPath, "uploads"), "CompanyLogos", folderName);
            Directory.CreateDirectory(folder);

            var fileName = SafeUpload.FileName(logo, SafeUpload.ImageExtensions);
            await using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.CreateNew))
            {
                await logo.CopyToAsync(stream);
            }
            return $"/uploads/CompanyLogos/{folderName}/{fileName}";
        }
    }
}
