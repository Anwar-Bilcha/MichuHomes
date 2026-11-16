using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Utilities;

namespace RadiantHomes.Pages.ApartmentHouses
{
    /// <summary>
    /// What the apartment Create and Edit pages share: dropdowns, numeric checks, photo storage and price text.
    /// </summary>
    /// <remarks>
    /// Apartment photos live on disk in wwwroot/uploads/apartmentphotos/{apartmentId}/ (the Details page lists that
    /// folder). They are not stored in ApartmentPhotos: that table's HouseId column has foreign keys to ApartmentHouses,
    /// Buildings AND Projects at once, so no row can satisfy all three.
    /// </remarks>
    public static partial class ApartmentHouseForm
    {
        public const string DefaultCurrency = "ETB";

        /// <summary>Currencies offered on the forms (ETB first: it is what the existing listings use).</summary>
        public static readonly string[] Currencies = { "ETB", "USD", "EUR" };

        /// <summary>Values of the "Available for" select (the stored values; "Sell" is shown as "Sale").</summary>
        public static readonly (string Value, string Text)[] AvailableForOptions = { ("Rent", "Rent"), ("Sell", "Sale"), ("Both", "Both") };

        /// <summary>"1,250,000 USD": the stored currency, or ETB when the apartment has none.</summary>
        public static string Price(double amount, string? currency) => $"{amount:N0} {CurrencyOrDefault(currency)}";

        public static string CurrencyOrDefault(string? currency) => string.IsNullOrWhiteSpace(currency) ? DefaultCurrency : currency.Trim();

        // ----- Dropdowns -----

        /// <summary>
        /// Building / address / category dropdowns with readable names (ViewData keys match the view's asp-items).
        /// A Real Estate Admin is offered only their own company's buildings.
        /// </summary>
        public static void PopulateDropdowns(ApplicationDbContext context, ViewDataDictionary viewData, ApartmentHouse? apartment, CompanyScope scope)
        {
            var buildings = scope.Buildings(context.Buildings)
                .AsNoTracking()
                .Include(b => b.Project)
                .OrderBy(b => b.BuildingName)
                .Select(b => new { b.Id, Name = b.Project != null ? b.BuildingName + " (" + b.Project.ProjectName + ")" : b.BuildingName })
                .ToList();
            viewData["BuildingId"] = new SelectList(buildings, "Id", "Name", apartment?.BuildingId);

            var addresses = context.Addresses.AsNoTracking().ToList()
                .Select(a => new { a.AddressID, Text = a.FormattedAddressCompact })
                .OrderBy(a => a.Text)
                .ToList();
            viewData["AddressId"] = new SelectList(addresses, "AddressID", "Text", apartment?.AddressId);

            viewData["HouseCategoryId"] = new SelectList(context.HouseCategories.AsNoTracking().OrderBy(c => c.Name).ToList(), "Id", "Name", apartment?.HouseCategoryId);
        }

        /// <summary>Currency options, keeping a stored value that is not in the standard list so an edit doesn't change it.</summary>
        public static IEnumerable<SelectListItem> CurrencyItems(string? current)
        {
            var values = Currencies.ToList();
            if (!string.IsNullOrWhiteSpace(current) && !values.Contains(current.Trim(), StringComparer.OrdinalIgnoreCase))
                values.Add(current.Trim());
            return values.Select(v => new SelectListItem(v, v));
        }

        // ----- Validation -----

        /// <summary>
        /// Removes the errors of properties the forms never post (navigations and computed values), and replaces the
        /// "The value '' is invalid." binding errors of empty dropdowns with real existence checks.
        /// </summary>
        /// <remarks>The building must be one of the user's own (a Real Estate Admin's company's) buildings.</remarks>
        public static async Task ValidateAsync(ApplicationDbContext context, ModelStateDictionary modelState, ApartmentHouse apartment, CompanyScope scope)
        {
            const string p = nameof(ApartmentHouse) + ".";
            foreach (var navigation in new[] { "ContactPerson", "HouseCategory", "HouseAddress", "HouseSpecs", "Building", "Photos",
                                               "Project", "HousingCompany", "RealEstateName", "SaleTransactions", "IssuesOrReportings", "Reviews" })
            {
                modelState.Remove(p + navigation);
            }

            foreach (var key in new[] { nameof(ApartmentHouse.BuildingId), nameof(ApartmentHouse.HouseCategoryId), nameof(ApartmentHouse.AddressId) })
                modelState.Remove(p + key);
            if (!await scope.CanAccessBuildingAsync(context, apartment.BuildingId))
                modelState.AddModelError(p + nameof(ApartmentHouse.BuildingId), "Please select a building.");
            if (!await context.HouseCategories.AnyAsync(c => c.Id == apartment.HouseCategoryId))
                modelState.AddModelError(p + nameof(ApartmentHouse.HouseCategoryId), "Please select a house category.");
            if (!await context.Addresses.AnyAsync(a => a.AddressID == apartment.AddressId))
                modelState.AddModelError(p + nameof(ApartmentHouse.AddressId), "Please select an address.");

            // The model has no range attributes, so the numbers are checked here (the inputs carry the same limits).
            CheckRange(modelState, p + nameof(ApartmentHouse.SalePrice), apartment.SalePrice, 0, 1_000_000_000_000, "Sale price",
                       "Sale price can't be negative or larger than 1,000,000,000,000.");
            CheckRange(modelState, p + nameof(ApartmentHouse.FloorNumber), apartment.FloorNumber, -5, 200, "Floor number");
            CheckRange(modelState, p + nameof(ApartmentHouse.Bedrooms), apartment.Bedrooms, 0, 50, "Bedrooms");
            CheckRange(modelState, p + nameof(ApartmentHouse.TotalRooms), apartment.TotalRooms, 1, 200, "Total rooms");
            CheckRange(modelState, p + nameof(ApartmentHouse.Bathrooms), apartment.Bathrooms, 0, 50, "Bathrooms");
            CheckRange(modelState, p + nameof(ApartmentHouse.LivingRooms), apartment.LivingRooms, 0, 50, "Living rooms");
            CheckRange(modelState, p + nameof(ApartmentHouse.Area), apartment.Area, 0, 100_000, "Area");
            if (IsClean(modelState, p + nameof(ApartmentHouse.TotalRooms)) && IsClean(modelState, p + nameof(ApartmentHouse.Bedrooms))
                && apartment.TotalRooms < apartment.Bedrooms)
            {
                modelState.AddModelError(p + nameof(ApartmentHouse.TotalRooms), "Total rooms can't be fewer than the number of bedrooms.");
            }

            if (!string.IsNullOrWhiteSpace(apartment.HouseType) && !Enum.GetNames<HouseTypesCateg>().Contains(apartment.HouseType))
                modelState.AddModelError(p + nameof(ApartmentHouse.HouseType), "Please select a house type.");
        }

        private static bool IsClean(ModelStateDictionary modelState, string key) =>
            !modelState.TryGetValue(key, out var entry) || entry.Errors.Count == 0;

        private static void CheckRange(ModelStateDictionary modelState, string key, double value, double min, double max, string label,
                                       string? message = null)
        {
            // A value that didn't bind already has its own error.
            if (!IsClean(modelState, key)) return;
            if (value < min || value > max)
                modelState.AddModelError(key, message ?? $"{label} must be between {min:N0} and {max:N0}.");
        }

        // ----- Photos -----

        /// <summary>
        /// The photo rows of the form, posted as Photos[i].PhotoFile / Photos[i].Description (rows can be removed
        /// client-side, so the indexes are not necessarily contiguous). Invalid files become model errors.
        /// </summary>
        public static List<(IFormFile File, string Description)> CollectPhotoUploads(HttpRequest request, ModelStateDictionary modelState)
        {
            var uploads = new List<(IFormFile File, string Description)>();
            if (!request.HasFormContentType) return uploads;

            foreach (var file in request.Form.Files)
            {
                var match = PhotoFieldName().Match(file.Name);
                if (!match.Success || file.Length == 0) continue; // an empty row

                var error = SafeUpload.Validate(file, SafeUpload.ImageExtensions);
                if (error != null)
                {
                    modelState.AddModelError(string.Empty, error);
                    continue;
                }
                uploads.Add((file, request.Form[$"Photos[{match.Groups[1].Value}].Description"].ToString()));
            }
            return uploads;
        }

        /// <summary>wwwroot/uploads/apartmentphotos/{apartmentId}</summary>
        public static string PhotoFolder(IWebHostEnvironment env, Guid apartmentId) =>
            SafeUpload.Under(Path.Combine(env.WebRootPath, "uploads"), "apartmentphotos", apartmentId.ToString());

        /// <summary>
        /// Saves one photo as "{description-slug}_{32 hex}.ext" (the Details page turns the slug back into alt text).
        /// </summary>
        public static async Task SavePhotoAsync(IWebHostEnvironment env, Guid apartmentId, IFormFile file, string? description)
        {
            var folder = PhotoFolder(env, apartmentId);
            Directory.CreateDirectory(folder);

            var slug = NonAlphanumeric().Replace((description ?? string.Empty).Trim().ToLowerInvariant(), "-").Trim('-');
            if (slug.Length > 60) slug = slug[..60].Trim('-');
            var fileName = $"{SafeUpload.Segment(slug, "photo")}_{Guid.NewGuid():N}{SafeUpload.AllowedExtension(file, SafeUpload.ImageExtensions)}";

            await using var stream = new FileStream(Path.Combine(folder, fileName), FileMode.CreateNew);
            await file.CopyToAsync(stream);
        }

        /// <summary>The apartment's own photos (file name + URL), oldest first.</summary>
        public static List<(string FileName, string Url)> ListPhotos(IWebHostEnvironment env, Guid apartmentId)
        {
            var folder = PhotoFolder(env, apartmentId);
            if (!Directory.Exists(folder)) return new();
            return Directory.GetFiles(folder)
                .Where(f => SafeUpload.ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => File.GetCreationTimeUtc(f))
                .Select(f => Path.GetFileName(f))
                .Select(name => (name, $"/uploads/apartmentphotos/{apartmentId}/{name}"))
                .ToList();
        }

        /// <summary>Deletes one of the apartment's own photos; the name must be a plain file name in its folder.</summary>
        public static bool DeletePhoto(IWebHostEnvironment env, Guid apartmentId, string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !PhotoFileName().IsMatch(fileName)) return false;

            var folder = PhotoFolder(env, apartmentId);
            var path = Path.GetFullPath(Path.Combine(folder, fileName));
            if (!string.Equals(Path.GetDirectoryName(path), folder, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                return false;

            File.Delete(path);
            return true;
        }

        /// <summary>Photo alt text from a file name made by <see cref="SavePhotoAsync"/>.</summary>
        public static string PhotoAlt(string path, string? title)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var cut = name.LastIndexOf('_');
            var slug = cut > 0 && name.Length - cut - 1 == 32 ? name[..cut] : null;
            return string.IsNullOrEmpty(slug) || slug == "photo"
                ? $"Photo of {title}"
                : $"{title}: {slug.Replace('-', ' ')}";
        }

        [GeneratedRegex(@"^Photos\[(\d+)\]\.PhotoFile$")]
        private static partial Regex PhotoFieldName();

        [GeneratedRegex(@"[^a-z0-9]+")]
        private static partial Regex NonAlphanumeric();

        [GeneratedRegex(@"^[A-Za-z0-9_\-]+\.(jpg|jpeg|png|gif|webp)$", RegexOptions.IgnoreCase)]
        private static partial Regex PhotoFileName();
    }
}
