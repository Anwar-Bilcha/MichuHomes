using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public EditModel(ApplicationDbContext context, UserManager<AppUser> UserManager, IWebHostEnvironment webHostEnvironment)
        {
            _userManager = UserManager;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty] public House House { get; set; } = default!;

        /// <summary>Admins (Michu Admin / Real Estate Admin) may move a house to another owner; owners can't.</summary>
        public bool CanChooseOwner { get; private set; }

        /// <summary>Current owner's name, shown when the owner can't be changed.</summary>
        public string? OwnerName { get; private set; }

        // Optional uploads: an empty input keeps the stored file.
        [BindProperty, Display(Name = "Ownership document")] public IFormFile? OwnershipDocument { get; set; }
        [BindProperty, Display(Name = "Consent document")] public IFormFile? ConsentDocument { get; set; }
        [BindProperty, Display(Name = "Tax voucher")] public IFormFile? TaxVoucher { get; set; }
        [BindProperty] public IFormFile? MainPhotoUrl { get; set; }
        [BindProperty] public IFormFile? OutOfCompoundViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile? FrontCompoundViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile? HouseInternalViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile? MainSalonViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile? MasterBedRoomViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile? KitchenViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile? RestRoomViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile? AuxiSalonViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile? SecBedRoomViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile? AdditionalViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile? BackyardViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile? Additional2PhotoUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null || _context.Houses == null)
            {
                return NotFound();
            }

            var house = await _context.Houses.AsNoTracking()
                .Include(h => h.HouseOwner)
                .Include(h => h.HouseRentInfo)
                .FirstOrDefaultAsync(h => h.Id == id);
            if (house == null)
            {
                return NotFound();
            }
            // Block access if the user is not the owner and not an admin
            if (!await HouseAccess.CanManageAsync(_userManager, User, house))
            {
                return Forbid();
            }
            House = house;
            House.AvailableFor = HouseAvailability.Normalize(House.AvailableFor);
            House.HouseRentInfo ??= new RentInfo(); // empty rent-terms fields, shown when switching to "Rent"
            CanChooseOwner = HouseAccess.CanChooseOwner(User);
            OwnerName = DisplayText.PersonName(house.HouseOwner);
            LoadSelectLists();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var houseToUpdate = await _context.Houses
                .Include(h => h.HouseRentInfo)
                .FirstOrDefaultAsync(h => h.Id == House.Id);
            if (houseToUpdate == null)
            {
                return NotFound();
            }
            if (!await HouseAccess.CanManageAsync(_userManager, User, houseToUpdate))
            {
                return Forbid();
            }
            CanChooseOwner = HouseAccess.CanChooseOwner(User);
            OwnerName = await _context.UserProfiles.AsNoTracking()
                .Where(c => c.Id == houseToUpdate.HouseOwnerId)
                .Select(c => (c.FirstName + " " + c.LastName).Trim())
                .FirstOrDefaultAsync();
            var isForRent = HouseAvailability.IsForRent(House.AvailableFor);

            // Navigation properties and the stored file links aren't posted by this form.
            HouseFormValidation.IgnoreNavigationProperties(ModelState, nameof(House),
                nameof(House.HouseCategory), nameof(House.HouseAddress), nameof(House.HouseOwner), nameof(House.HouseHolder),
                nameof(House.SaleTransactions), nameof(House.RentTransactions), nameof(House.IssuesOrReportings), nameof(House.Reviews),
                nameof(House.HouseRentInfo));
            // The rent terms (same fields as Houses/Create) only count for rent listings.
            ModelState.Remove($"{nameof(House)}.{nameof(House.HouseRentInfo)}.{nameof(RentInfo.House)}");
            if (!isForRent)
            {
                foreach (var key in ModelState.Keys.Where(k => k.StartsWith($"{nameof(House)}.{nameof(House.HouseRentInfo)}.")).ToList())
                {
                    ModelState.Remove(key);
                }
            }
            void RequireChoice(bool missing, string key, string message)
            {
                if (!missing) return;
                ModelState.Remove(key); // replaces the generic "The value '' is invalid."
                ModelState.AddModelError(key, message);
            }
            if (CanChooseOwner)
            {
                RequireChoice(House.HouseOwnerId == Guid.Empty || !await _context.UserProfiles.AnyAsync(c => c.Id == House.HouseOwnerId),
                    "House.HouseOwnerId", "Please choose the owner.");
            }
            else
            {
                // Only admins can move a house to another owner: ignore whatever was posted.
                ModelState.Remove($"{nameof(House)}.{nameof(House.HouseOwnerId)}");
                House.HouseOwnerId = houseToUpdate.HouseOwnerId;
            }
            RequireChoice(House.HouseCategoryId == Guid.Empty, "House.HouseCategoryId", "Please choose a category.");
            RequireChoice(House.AddressId == Guid.Empty, "House.AddressId", "Please choose an address.");
            RequireChoice(string.IsNullOrEmpty(House.AvailableFor), "House.AvailableFor", "Please choose whether the house is for rent or sale.");
            RequireChoice(isForRent && string.IsNullOrEmpty(Request.Form["House.HouseRentInfo.RentTerm"]),
                "House.HouseRentInfo.RentTerm", "Please choose the rent term.");
            var photos = new (string Key, IFormFile? File)[]
            {
                (nameof(MainPhotoUrl), MainPhotoUrl), (nameof(OutOfCompoundViewPhotoUrl), OutOfCompoundViewPhotoUrl),
                (nameof(FrontCompoundViewPhotoUrl), FrontCompoundViewPhotoUrl), (nameof(HouseInternalViewPhotoUrl), HouseInternalViewPhotoUrl),
                (nameof(MainSalonViewPhotoUrl), MainSalonViewPhotoUrl), (nameof(MasterBedRoomViewPhotoUrl), MasterBedRoomViewPhotoUrl),
                (nameof(KitchenViewPhotoUrl), KitchenViewPhotoUrl), (nameof(RestRoomViewPhotoUrl), RestRoomViewPhotoUrl),
                (nameof(AuxiSalonViewPhotoUrl), AuxiSalonViewPhotoUrl), (nameof(SecBedRoomViewPhotoUrl), SecBedRoomViewPhotoUrl),
                (nameof(AdditionalViewPhotoUrl), AdditionalViewPhotoUrl), (nameof(BackyardViewPhotoUrl), BackyardViewPhotoUrl),
                (nameof(Additional2PhotoUrl), Additional2PhotoUrl)
            };
            foreach (var (key, file) in photos)
            {
                UploadedFiles.Validate(ModelState, key, file, UploadedFiles.ImageExtensions);
            }
            UploadedFiles.Validate(ModelState, nameof(OwnershipDocument), OwnershipDocument, UploadedFiles.DocumentExtensions);
            UploadedFiles.Validate(ModelState, nameof(ConsentDocument), ConsentDocument, UploadedFiles.DocumentExtensions);
            UploadedFiles.Validate(ModelState, nameof(TaxVoucher), TaxVoucher, UploadedFiles.DocumentExtensions);

            if (!ModelState.IsValid)
            {
                CopyStoredFileLinks(houseToUpdate, House);
                House.HouseRentInfo ??= new RentInfo();
                LoadSelectLists();
                return Page();
            }

            // Replace a stored file only when a new one was uploaded.
            var webRoot = _webHostEnvironment.WebRootPath;
            var folder = $"uploads/houses/{houseToUpdate.Id}";
            async Task<string?> ReplaceAsync(IFormFile? file, string? currentUrl, string baseName)
            {
                if (file is not { Length: > 0 }) return currentUrl;
                UploadedFiles.DeleteIfExists(webRoot, currentUrl);
                return await UploadedFiles.SaveAsync(file, webRoot, folder, baseName);
            }

            houseToUpdate.MainPhotoUrl = await ReplaceAsync(MainPhotoUrl, houseToUpdate.MainPhotoUrl, "MainPhoto");
            houseToUpdate.OutOfCompoundViewPhotoUrl = await ReplaceAsync(OutOfCompoundViewPhotoUrl, houseToUpdate.OutOfCompoundViewPhotoUrl, "OutOfCompoundView");
            houseToUpdate.FrontCompoundViewPhotoUrl = await ReplaceAsync(FrontCompoundViewPhotoUrl, houseToUpdate.FrontCompoundViewPhotoUrl, "FrontCompoundView");
            houseToUpdate.HouseInternalViewPhotoUrl = await ReplaceAsync(HouseInternalViewPhotoUrl, houseToUpdate.HouseInternalViewPhotoUrl, "HouseInternalView");
            houseToUpdate.MainSalonViewPhotoUrl = await ReplaceAsync(MainSalonViewPhotoUrl, houseToUpdate.MainSalonViewPhotoUrl, "MainSalonView");
            houseToUpdate.MasterBedRoomViewPhotoUrl = await ReplaceAsync(MasterBedRoomViewPhotoUrl, houseToUpdate.MasterBedRoomViewPhotoUrl, "MasterBedRoomView");
            houseToUpdate.KitchenViewPhotoUrl = await ReplaceAsync(KitchenViewPhotoUrl, houseToUpdate.KitchenViewPhotoUrl, "KitchenView");
            houseToUpdate.RestRoomViewPhotoUrl = await ReplaceAsync(RestRoomViewPhotoUrl, houseToUpdate.RestRoomViewPhotoUrl, "RestRoomView");
            houseToUpdate.AuxiSalonViewPhotoUrl = await ReplaceAsync(AuxiSalonViewPhotoUrl, houseToUpdate.AuxiSalonViewPhotoUrl, "AuxiSalonView");
            houseToUpdate.SecBedRoomViewPhotoUrl = await ReplaceAsync(SecBedRoomViewPhotoUrl, houseToUpdate.SecBedRoomViewPhotoUrl, "SecBedRoomView");
            houseToUpdate.AdditionalViewPhotoUrl = await ReplaceAsync(AdditionalViewPhotoUrl, houseToUpdate.AdditionalViewPhotoUrl, "AdditionalView");
            houseToUpdate.BackyardViewPhotoUrl = await ReplaceAsync(BackyardViewPhotoUrl, houseToUpdate.BackyardViewPhotoUrl, "BackyardView");
            houseToUpdate.Additional2PhotoUrl = await ReplaceAsync(Additional2PhotoUrl, houseToUpdate.Additional2PhotoUrl, "Additional2Photo");
            houseToUpdate.OwnershipDocumentUrl = await ReplaceAsync(OwnershipDocument, houseToUpdate.OwnershipDocumentUrl, "OwnershipDocument");
            houseToUpdate.ConsentofSaleOrRentDocUrl = await ReplaceAsync(ConsentDocument, houseToUpdate.ConsentofSaleOrRentDocUrl, "ConsentDocument");
            houseToUpdate.LatestHouseTaxVoucherUrl = await ReplaceAsync(TaxVoucher, houseToUpdate.LatestHouseTaxVoucherUrl, "TaxVoucher");

            // Update the fields this form edits (Status and the house holder aren't on the form, so they're kept).
            houseToUpdate.Title = House.Title;
            houseToUpdate.Description = House.Description;
            houseToUpdate.HouseOwnerId = House.HouseOwnerId;
            houseToUpdate.Price = House.Price;
            houseToUpdate.TotalBedRooms = House.TotalBedRooms;
            houseToUpdate.TotalRooms = House.TotalRooms;
            houseToUpdate.TotalArea = House.TotalArea;
            houseToUpdate.HasOwnershipDocument = House.HasOwnershipDocument;
            houseToUpdate.HouseCategoryId = House.HouseCategoryId;
            houseToUpdate.AvailableFor = HouseAvailability.Normalize(House.AvailableFor);
            houseToUpdate.AddressId = House.AddressId;
            houseToUpdate.HouseOwnership = House.HouseOwnership;
            // UpdatedBy/UpdatedOn are stamped by ApplicationDbContext.SaveChanges.

            // Rent terms: create them the first time the house is listed for rent, update them afterwards.
            // Switching to "Sale" keeps the stored terms (and the on-rent flag) for a later switch back.
            if (isForRent)
            {
                var posted = House.HouseRentInfo ?? new RentInfo();
                var rentInfo = houseToUpdate.HouseRentInfo;
                if (rentInfo is null)
                {
                    rentInfo = new RentInfo { HouseId = houseToUpdate.Id, IsOnRent = false };
                    houseToUpdate.HouseRentInfo = rentInfo;
                }
                rentInfo.RentTerm = posted.RentTerm;
                rentInfo.PricePerTerm = posted.PricePerTerm;
                rentInfo.ExcepectedAdvancePayment = posted.ExcepectedAdvancePayment;
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = "House updated successfully!";
                return RedirectToPage("./Index");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HouseExists(House.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>When the form is shown again after a validation error, keep showing the stored files.</summary>
        private static void CopyStoredFileLinks(House from, House to)
        {
            to.MainPhotoUrl = from.MainPhotoUrl;
            to.OutOfCompoundViewPhotoUrl = from.OutOfCompoundViewPhotoUrl;
            to.FrontCompoundViewPhotoUrl = from.FrontCompoundViewPhotoUrl;
            to.HouseInternalViewPhotoUrl = from.HouseInternalViewPhotoUrl;
            to.MainSalonViewPhotoUrl = from.MainSalonViewPhotoUrl;
            to.MasterBedRoomViewPhotoUrl = from.MasterBedRoomViewPhotoUrl;
            to.KitchenViewPhotoUrl = from.KitchenViewPhotoUrl;
            to.RestRoomViewPhotoUrl = from.RestRoomViewPhotoUrl;
            to.AuxiSalonViewPhotoUrl = from.AuxiSalonViewPhotoUrl;
            to.SecBedRoomViewPhotoUrl = from.SecBedRoomViewPhotoUrl;
            to.AdditionalViewPhotoUrl = from.AdditionalViewPhotoUrl;
            to.BackyardViewPhotoUrl = from.BackyardViewPhotoUrl;
            to.Additional2PhotoUrl = from.Additional2PhotoUrl;
            to.OwnershipDocumentUrl = from.OwnershipDocumentUrl;
            to.ConsentofSaleOrRentDocUrl = from.ConsentofSaleOrRentDocUrl;
            to.LatestHouseTaxVoucherUrl = from.LatestHouseTaxVoucherUrl;
        }

        private void LoadSelectLists()
        {
            ViewData["AddressId"] = HouseSelectLists.Addresses(_context, House.AddressId);
            ViewData["HouseCategoryId"] = HouseSelectLists.Categories(_context, House.HouseCategoryId);
            if (CanChooseOwner)
            {
                ViewData["HouseOwnerId"] = HouseSelectLists.Customers(_context, House.HouseOwnerId);
            }
        }

        private bool HouseExists(Guid id)
        {
            return _context.Houses.Any(e => e.Id == id);
        }
    }
}
