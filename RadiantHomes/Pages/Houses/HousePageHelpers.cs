using System.Globalization;
using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Shared;
using RadiantHomes.Utilities;

namespace RadiantHomes.Pages.Houses
{
    /// <summary>Who may do what with an individual house (one place, so pages and their buttons agree).</summary>
    public static class HouseAccess
    {
        /// <summary>
        /// Platform and real-estate admins may list a house for any owner; everyone else lists houses as
        /// themselves (their own customer profile).
        /// </summary>
        public static bool CanChooseOwner(ClaimsPrincipal user) =>
            user.IsInRole(MichuHomesRoles.MichuAdmin) || user.IsInRole(MichuHomesRoles.RealEstateAdmin);

        /// <summary>
        /// The customer profile linked to the signed-in account (AppUser.CustomerId), or null.
        /// Read from the account rather than the session: the session value can be stale or missing.
        /// </summary>
        public static async Task<Guid?> CurrentCustomerIdAsync(UserManager<AppUser> userManager, ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true) return null;
            var appUser = await userManager.GetUserAsync(user);
            return appUser?.CustomerId is Guid id && id != Guid.Empty ? id : null;
        }

        /// <summary>The house owner or a Michu admin may edit or delete a house.</summary>
        public static bool CanManage(ClaimsPrincipal user, Guid? currentCustomerId, House house) =>
            user.IsInRole(MichuHomesRoles.MichuAdmin) ||
            (currentCustomerId is Guid customerId && customerId == house.HouseOwnerId);

        public static async Task<bool> CanManageAsync(UserManager<AppUser> userManager, ClaimsPrincipal user, House house) =>
            user.IsInRole(MichuHomesRoles.MichuAdmin) ||
            CanManage(user, await CurrentCustomerIdAsync(userManager, user), house);
    }

    /// <summary>
    /// Who may see or change a rent/sale transaction or a rent payment: a Michu admin, or a party to it
    /// (rent: owner or tenant; sale: seller or buyer; payment: the parties of its rent transaction),
    /// matched on the signed-in account's customer profile (AppUser.CustomerId).
    /// </summary>
    public static class TransactionAccess
    {
        public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(MichuHomesRoles.MichuAdmin);

        public static bool IsParty(Guid? customerId, Guid ownerId, Guid tenantOrBuyerId) =>
            customerId is Guid id && (id == ownerId || id == tenantOrBuyerId);

        public static async Task<bool> CanAccessAsync(UserManager<AppUser> userManager, ClaimsPrincipal user, Guid ownerId, Guid tenantOrBuyerId) =>
            IsAdmin(user) || IsParty(await HouseAccess.CurrentCustomerIdAsync(userManager, user), ownerId, tenantOrBuyerId);

        /// <summary>Everything for a Michu admin; otherwise only the rows the customer is a party to (none without a profile).</summary>
        public static IQueryable<HouseRentTransaction> VisibleTo(this IQueryable<HouseRentTransaction> query, bool isAdmin, Guid? customerId) =>
            isAdmin ? query
            : customerId is Guid id ? query.Where(t => t.OwnerId == id || t.CustomerId == id)
            : query.Where(t => false);

        public static IQueryable<HouseSaleTransaction> VisibleTo(this IQueryable<HouseSaleTransaction> query, bool isAdmin, Guid? customerId) =>
            isAdmin ? query
            : customerId is Guid id ? query.Where(t => t.OwnerId == id || t.CustomerId == id)
            : query.Where(t => false);

        public static IQueryable<HouseRentPayment> VisibleTo(this IQueryable<HouseRentPayment> query, bool isAdmin, Guid? customerId) =>
            isAdmin ? query
            : customerId is Guid id ? query.Where(p => p.Transaction.OwnerId == id || p.Transaction.CustomerId == id)
            : query.Where(p => false);
    }

    /// <summary>
    /// Who may see or change a complaint (IssueOrCompliant). Michu admins: everything. The owner of the house it is
    /// about may see it. The complainant should also see it and be the one who edits or deletes it, but the
    /// IssuesOrCompliants table has no column linking a complaint to the customer who filed it (only the free-text
    /// name and contact details), so the complainant can't be recognised without a schema change. Until such a
    /// column exists <see cref="IsComplainant"/> is always false: only Michu admins edit or delete complaints.
    /// </summary>
    public static class ComplaintAccess
    {
        /// <summary>Always false for now (no complainant column): see the class summary.</summary>
        public static bool IsComplainant(IssueOrCompliant issue, Guid? customerId) => false;

        /// <summary>The owner recorded on the complaint, or the house's current owner.</summary>
        public static bool IsHouseOwner(IssueOrCompliant issue, Guid? customerId) =>
            customerId is Guid id && (issue.HouseOwnerId == id || issue.House?.HouseOwnerId == id);

        /// <summary>Needs <c>issue.House</c> loaded to recognise the house's current owner.</summary>
        public static bool CanView(ClaimsPrincipal user, Guid? customerId, IssueOrCompliant issue) =>
            TransactionAccess.IsAdmin(user) || IsComplainant(issue, customerId) || IsHouseOwner(issue, customerId);

        public static bool CanChange(ClaimsPrincipal user, Guid? customerId, IssueOrCompliant issue) =>
            TransactionAccess.IsAdmin(user) || IsComplainant(issue, customerId);

        /// <summary>Everything for a Michu admin; otherwise the complaints about the customer's own houses.</summary>
        public static IQueryable<IssueOrCompliant> VisibleTo(this IQueryable<IssueOrCompliant> query, bool isAdmin, Guid? customerId) =>
            isAdmin ? query
            : customerId is Guid id ? query.Where(c => c.HouseOwnerId == id || c.House.HouseOwnerId == id)
            : query.Where(c => false);
    }

    /// <summary>Created/Updated by/on are stamped by ApplicationDbContext.SaveChanges; forms never set them.</summary>
    public static class AuditFields
    {
        /// <summary>
        /// Clears any posted audit values on a new record, so the context stamps the real ones
        /// (it keeps a CreatedBy/CreatedOn that is already set).
        /// </summary>
        public static void ResetForInsert(BaseEntity entity)
        {
            entity.CreatedBy = null;
            entity.CreatedOn = default;
            entity.UpdatedBy = null;
            entity.UpdatedOn = null;
        }
    }

    /// <summary>Which apartments are publicly listed (the apartment listing and the home page show only these).</summary>
    public static class ApartmentListing
    {
        /// <summary>Approved by an admin and still available.</summary>
        public static readonly Expression<Func<ApartmentHouse, bool>> IsListed =
            h => h.IsAvailable && h.Status != null && h.Status.ToLower() == "approved";
    }

    /// <summary>
    /// The cover photo of an apartment, from the same folders ApartmentHouses/Details shows: the apartment's own
    /// uploads (wwwroot/uploads/apartmentphotos/{id}) first, then the company's shared photos for that bedroom count
    /// (Configurations.ApartmentHousePhotosFolder/{first word of the company name}/{n}bed); oldest file first.
    /// Use one instance per request: folder listings are cached, so a page of cards costs one directory listing
    /// per distinct folder instead of one per card.
    /// </summary>
    public sealed class ApartmentCoverPhotos
    {
        private readonly string _webRoot;
        private readonly string _ownPhotosRoot;
        private readonly string _sharedPhotosRoot;
        private readonly Dictionary<string, string?> _firstPhotoByFolder = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string>? _apartmentsWithOwnPhotos;

        public ApartmentCoverPhotos(IWebHostEnvironment env)
        {
            Configurations.Initialize(env);
            _webRoot = env.WebRootPath;
            _ownPhotosRoot = Path.Combine(_webRoot, "uploads", "apartmentphotos");
            _sharedPhotosRoot = Configurations.ApartmentHousePhotosFolder;
        }

        /// <summary>URL of the cover photo, or null when the apartment has none.</summary>
        public string? Find(Guid apartmentId, string? companyName, int bedrooms)
        {
            // One listing of the per-apartment upload folders instead of a Directory.Exists per card.
            _apartmentsWithOwnPhotos ??= Directory.Exists(_ownPhotosRoot)
                ? Directory.EnumerateDirectories(_ownPhotosRoot).Select(d => Path.GetFileName(d)).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_apartmentsWithOwnPhotos.Contains(apartmentId.ToString())
                && FirstPhoto(Path.Combine(_ownPhotosRoot, apartmentId.ToString())) is string own)
            {
                return own;
            }

            var company = companyName?.Trim().Split(' ')[0];
            if (string.IsNullOrEmpty(company)) return null;
            // SafeUpload.Under keeps the path inside the photos folder whatever the company is called.
            return FirstPhoto(SafeUpload.Under(_sharedPhotosRoot, company, $"{bedrooms}bed"));
        }

        private string? FirstPhoto(string folder)
        {
            if (_firstPhotoByFolder.TryGetValue(folder, out var cached)) return cached;

            string? url = null;
            if (Directory.Exists(folder))
            {
                var first = new DirectoryInfo(folder).EnumerateFiles()
                    .Where(f => SafeUpload.ImageExtensions.Contains(f.Extension.ToLowerInvariant()))
                    .OrderBy(f => f.CreationTimeUtc)
                    .FirstOrDefault();
                if (first is not null)
                {
                    var segments = Path.GetRelativePath(_webRoot, first.FullName)
                        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Select(Uri.EscapeDataString);
                    url = "/" + string.Join('/', segments);
                }
            }
            return _firstPhotoByFolder[folder] = url;
        }
    }

    /// <summary>
    /// The values stored in <see cref="House.AvailableFor"/> and helpers to read them.
    /// "Sell" is what the Create form, the home page and the apartment pages store; older versions of the
    /// house Edit form stored "Sale", so both spellings are treated as "for sale".
    /// </summary>
    public static class HouseAvailability
    {
        public const string Rent = "Rent";
        public const string Sale = "Sell";

        public static bool IsForRent(string? availableFor) =>
            string.Equals(availableFor?.Trim(), Rent, StringComparison.OrdinalIgnoreCase);

        public static bool IsForSale(string? availableFor) =>
            availableFor?.Trim().ToLowerInvariant() is "sell" or "sale";

        /// <summary>The canonical stored value ("Rent" / "Sell"), or the input unchanged when it is neither.</summary>
        public static string? Normalize(string? availableFor) =>
            IsForRent(availableFor) ? Rent : IsForSale(availableFor) ? Sale : availableFor;

        /// <summary>Human-readable label ("Rent" / "Sale").</summary>
        public static string Label(string? availableFor) =>
            IsForRent(availableFor) ? "Rent" : IsForSale(availableFor) ? "Sale" : availableFor ?? string.Empty;

        /// <summary>The price shown and filtered on: the rent per term for rent listings, otherwise the house price.</summary>
        public static decimal ListingPrice(House house) =>
            IsForRent(house.AvailableFor) && house.HouseRentInfo is { PricePerTerm: > 0 } rent
                ? (decimal)rent.PricePerTerm
                : house.Price;

        /// <summary>Culture-invariant number for data-* attributes read by JavaScript.</summary>
        public static string InvariantNumber(decimal value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>"Semi_Annually" → "Semi-annually" etc.</summary>
        public static string RentTermLabel(RentInfo.RentTerms term) => term switch
        {
            RentInfo.RentTerms.Semi_Annually => "Semi-annually",
            _ => term.ToString()
        };
    }

    /// <summary>Select lists with readable texts for the house forms.</summary>
    public static class HouseSelectLists
    {
        public static SelectList Addresses(ApplicationDbContext context, object? selected = null)
        {
            // FormattedAddressCompact is computed in C#, so materialise the rows first.
            var items = context.Addresses.AsNoTracking().ToList()
                .Select(a => new { a.AddressID, Text = a.FormattedAddressCompact })
                .OrderBy(a => a.Text)
                .ToList();
            return new SelectList(items, "AddressID", "Text", selected);
        }

        public static SelectList Categories(ApplicationDbContext context, object? selected = null) =>
            new(context.HouseCategories.AsNoTracking().OrderBy(c => c.Name).ToList(), "Id", "Name", selected);

        public static SelectList Customers(ApplicationDbContext context, object? selected = null)
        {
            var items = context.UserProfiles.AsNoTracking()
                .OrderBy(c => c.FirstName).ThenBy(c => c.LastName)
                .Select(c => new { c.Id, Text = (c.FirstName + " " + c.LastName).Trim() })
                .ToList();
            return new SelectList(items, "Id", "Text", selected);
        }

        public static SelectList Houses(ApplicationDbContext context, object? selected = null, bool forSaleOnly = false)
        {
            var houses = context.Houses.AsNoTracking();
            if (forSaleOnly)
            {
                houses = houses.Where(h => h.AvailableFor == HouseAvailability.Sale || h.AvailableFor == "Sale");
            }
            var items = houses
                .OrderBy(h => h.Title)
                .Select(h => new { h.Id, Text = h.Title + (h.HouseAddress != null ? " — " + h.HouseAddress.City : "") })
                .ToList();
            return new SelectList(items, "Id", "Text", selected);
        }

        /// <summary>Rent transactions as "House title — tenant name — date".</summary>
        public static SelectList RentTransactions(ApplicationDbContext context, object? selected = null)
        {
            var items = context.HouseRentTransactions.AsNoTracking()
                .OrderByDescending(t => t.TransactionDate)
                .Select(t => new
                {
                    t.Id,
                    House = t.House != null ? t.House.Title : null,
                    Tenant = t.Receiver != null ? (t.Receiver.FirstName + " " + t.Receiver.LastName).Trim() : null,
                    t.TransactionDate
                })
                .ToList()
                .Select(t => new
                {
                    t.Id,
                    Text = string.Join(" — ", new[] { t.House, t.Tenant, t.TransactionDate.ToString("d", CultureInfo.CurrentCulture) }
                        .Where(p => !string.IsNullOrWhiteSpace(p)))
                });
            return new SelectList(items, "Id", "Text", selected);
        }
    }

    /// <summary>Readable texts for related records (instead of their ids).</summary>
    public static class DisplayText
    {
        public static async Task<string> HouseTitleAsync(ApplicationDbContext context, Guid houseId) =>
            await context.Houses.AsNoTracking().Where(h => h.Id == houseId).Select(h => h.Title).FirstOrDefaultAsync() ?? string.Empty;

        public static async Task<string> PersonNameAsync(ApplicationDbContext context, Guid customerId) =>
            await context.UserProfiles.AsNoTracking().Where(c => c.Id == customerId)
                .Select(c => (c.FirstName + " " + c.LastName).Trim()).FirstOrDefaultAsync() ?? string.Empty;

        public static string PersonName(Customer? customer) =>
            customer is null ? string.Empty : $"{customer.FirstName} {customer.LastName}".Trim();

        /// <summary>"House title — tenant name" for a rent transaction (needs House and Receiver loaded).</summary>
        public static string RentTransaction(HouseRentTransaction? transaction)
        {
            if (transaction is null) return string.Empty;
            var parts = new[] { transaction.House?.Title, PersonName(transaction.Receiver) }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
            return parts.Count > 0
                ? string.Join(" — ", parts)
                : $"Rent transaction of {transaction.TransactionDate:d}";
        }
    }

    public static class HouseFormValidation
    {
        /// <summary>
        /// Nullable reference types make EF navigation properties implicitly [Required]; they are never posted by
        /// the forms, so drop their (always failing) validation entries before checking ModelState.
        /// </summary>
        public static void IgnoreNavigationProperties(ModelStateDictionary modelState, string prefix, params string[] navigationProperties)
        {
            foreach (var property in navigationProperties)
            {
                modelState.Remove($"{prefix}.{property}");
            }
        }
    }

    /// <summary>Saves uploaded files under wwwroot/uploads with a fixed name and an allow-listed extension.</summary>
    public static class UploadedFiles
    {
        public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        public static readonly string[] DocumentExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };
        public const long MaxBytes = 10 * 1024 * 1024;

        /// <summary>Adds a model error when the file is present but has a disallowed type or is too large.</summary>
        public static void Validate(ModelStateDictionary modelState, string key, IFormFile? file, string[] allowedExtensions)
        {
            if (file is null || file.Length == 0) return;
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                modelState.AddModelError(key, $"\"{file.FileName}\" is not an allowed file type ({string.Join(", ", allowedExtensions)}).");
            }
            else if (file.Length > MaxBytes)
            {
                modelState.AddModelError(key, $"\"{file.FileName}\" is larger than {MaxBytes / (1024 * 1024)} MB.");
            }
        }

        /// <summary>
        /// Writes the file to wwwroot/&lt;relativeFolder&gt;/&lt;baseName&gt;&lt;ext&gt; and returns its URL, or null when no file was posted.
        /// Call <see cref="Validate"/> first.
        /// </summary>
        public static async Task<string?> SaveAsync(IFormFile? file, string webRootPath, string relativeFolder, string baseName)
        {
            if (file is null || file.Length == 0) return null;

            var folder = Path.Combine(webRootPath, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(folder);
            var fileName = baseName + Path.GetExtension(file.FileName).ToLowerInvariant();
            await using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return $"/{relativeFolder.Trim('/')}/{fileName}";
        }

        /// <summary>Removes the upload folder of a record that could not be saved.</summary>
        public static void DeleteFolder(string webRootPath, string relativeFolder)
        {
            var folder = Path.Combine(webRootPath, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }

        /// <summary>Deletes a previously uploaded file (only paths under /uploads/).</summary>
        public static void DeleteIfExists(string webRootPath, string? url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)) return;
            var path = Path.GetFullPath(Path.Combine(webRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            var uploadsRoot = Path.GetFullPath(Path.Combine(webRootPath, "uploads"));
            if (path.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
