using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
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
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<AppUser> _userManager;
        public SelectList AddressList { get; set; } = default!;

        /// <summary>Admins (Michu Admin / Real Estate Admin) pick the owner; everyone else lists as themselves.</summary>
        public bool CanChooseOwner { get; private set; }

        /// <summary>Name of the signed-in user's own customer profile (shown instead of the owner picker).</summary>
        public string? OwnOwnerName { get; private set; }

        /// <summary>True when a non-admin account has no customer profile to list the house under.</summary>
        public bool ProfileMissing { get; private set; }

        public CreateModel(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, UserManager<AppUser> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            House = new House();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await ResolveOwnerAsync())
            {
                return Page(); // explains that a customer profile is needed first
            }
            LoadRequiredData();
            if (HouseAvailability.IsForRent(House.AvailableFor) && House.HouseRentInfo == null)
            {
                House.HouseRentInfo = new RentInfo();
            }
            return Page();
        }

        [BindProperty, Required(ErrorMessage = "Please choose a main photo of the house."), Display(Name = "Main house photo")]
        public IFormFile? MainPhotoUrl { get; set; }
        [BindProperty, Display(Name = "Tax voucher")] public IFormFile? TaxVoucherFile { get; set; }
        [BindProperty] public IFormFile? OutOfCompoundViewPhoto { get; set; }
        [BindProperty] public IFormFile? FrontCompoundViewPhoto { get; set; }
        [BindProperty] public IFormFile? HouseInternalViewPhoto { get; set; }
        [BindProperty] public IFormFile? MainSalonViewPhoto { get; set; }
        [BindProperty] public IFormFile? MasterBedRoomViewPhoto { get; set; }
        [BindProperty] public IFormFile? KitchenViewPhoto { get; set; }
        [BindProperty] public IFormFile? RestRoomViewPhoto { get; set; }
        [BindProperty] public IFormFile? AuxiSalonViewPhoto { get; set; }
        [BindProperty] public IFormFile? SecBedRoomViewPhoto { get; set; }
        [BindProperty] public IFormFile? AdditionalViewPhoto { get; set; }
        [BindProperty] public IFormFile? BackyardViewPhoto { get; set; }
        [BindProperty] public IFormFile? Additional2Photo { get; set; }

        [BindProperty]
        public House House { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            // The owner of a house listed by a non-admin is always their own profile, whatever was posted.
            ModelState.Remove($"{nameof(House)}.{nameof(House.HouseOwnerId)}");
            if (!await ResolveOwnerAsync())
            {
                return Page();
            }

            House.AvailableFor = HouseAvailability.Normalize(House.AvailableFor);
            var isForRent = HouseAvailability.IsForRent(House.AvailableFor);

            // Navigation properties are never posted; the rent terms are only relevant for rent listings.
            HouseFormValidation.IgnoreNavigationProperties(ModelState, nameof(House),
                nameof(House.HouseCategory), nameof(House.HouseAddress), nameof(House.HouseOwner), nameof(House.HouseHolder),
                nameof(House.SaleTransactions), nameof(House.RentTransactions), nameof(House.IssuesOrReportings), nameof(House.Reviews));
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
            RequireChoice(House.HouseOwnerId == Guid.Empty || (CanChooseOwner && !await _context.UserProfiles.AnyAsync(c => c.Id == House.HouseOwnerId)),
                "House.HouseOwnerId", "Please choose the owner.");
            RequireChoice(House.HouseCategoryId == Guid.Empty, "House.HouseCategoryId", "Please choose a category.");
            RequireChoice(House.AddressId == Guid.Empty, "House.AddressId", "Please choose an address.");
            RequireChoice(string.IsNullOrEmpty(House.AvailableFor), "House.AvailableFor", "Please choose whether the house is for rent or sale.");
            RequireChoice(isForRent && string.IsNullOrEmpty(Request.Form["House.HouseRentInfo.RentTerm"]),
                "House.HouseRentInfo.RentTerm", "Please choose the rent term.");

            var photos = new (string Key, IFormFile? File)[]
            {
                (nameof(MainPhotoUrl), MainPhotoUrl), (nameof(OutOfCompoundViewPhoto), OutOfCompoundViewPhoto),
                (nameof(FrontCompoundViewPhoto), FrontCompoundViewPhoto), (nameof(HouseInternalViewPhoto), HouseInternalViewPhoto),
                (nameof(MainSalonViewPhoto), MainSalonViewPhoto), (nameof(MasterBedRoomViewPhoto), MasterBedRoomViewPhoto),
                (nameof(KitchenViewPhoto), KitchenViewPhoto), (nameof(RestRoomViewPhoto), RestRoomViewPhoto),
                (nameof(AuxiSalonViewPhoto), AuxiSalonViewPhoto), (nameof(SecBedRoomViewPhoto), SecBedRoomViewPhoto),
                (nameof(AdditionalViewPhoto), AdditionalViewPhoto), (nameof(BackyardViewPhoto), BackyardViewPhoto),
                (nameof(Additional2Photo), Additional2Photo)
            };
            foreach (var (key, file) in photos)
            {
                UploadedFiles.Validate(ModelState, key, file, UploadedFiles.ImageExtensions);
            }
            UploadedFiles.Validate(ModelState, nameof(TaxVoucherFile), TaxVoucherFile, UploadedFiles.DocumentExtensions);

            if (!ModelState.IsValid)
            {
                LoadRequiredData();
                return Page();
            }

            // Generate the ids here: the EF model declares NEWID() defaults, but the database columns have none,
            // so leaving them to the database inserts NULL. A fixed id also lets the uploads be stored under it.
            House.Id = Guid.NewGuid();
            AuditFields.ResetForInsert(House); // stamped by ApplicationDbContext on save
            House.HouseHolderId = House.HouseOwnerId;
            House.HouseRentInfo = isForRent
                ? new RentInfo
                {
                    Id = Guid.NewGuid(),
                    HouseId = House.Id,
                    IsOnRent = false,
                    RentTerm = House.HouseRentInfo?.RentTerm ?? RentInfo.RentTerms.Monthly,
                    PricePerTerm = House.HouseRentInfo?.PricePerTerm ?? 0,
                    ExcepectedAdvancePayment = House.HouseRentInfo?.ExcepectedAdvancePayment ?? 0
                }
                : null;

            var webRoot = _webHostEnvironment.WebRootPath;
            var folder = $"uploads/houses/{House.Id}";
            House.MainPhotoUrl = await UploadedFiles.SaveAsync(MainPhotoUrl, webRoot, folder, "MainPhoto");
            House.LatestHouseTaxVoucherUrl = await UploadedFiles.SaveAsync(TaxVoucherFile, webRoot, folder, "TaxVoucher");
            House.FrontCompoundViewPhotoUrl = await UploadedFiles.SaveAsync(FrontCompoundViewPhoto, webRoot, folder, "FrontCompoundView");
            House.OutOfCompoundViewPhotoUrl = await UploadedFiles.SaveAsync(OutOfCompoundViewPhoto, webRoot, folder, "OutOfCompoundView");
            House.HouseInternalViewPhotoUrl = await UploadedFiles.SaveAsync(HouseInternalViewPhoto, webRoot, folder, "HouseInternalView");
            House.MainSalonViewPhotoUrl = await UploadedFiles.SaveAsync(MainSalonViewPhoto, webRoot, folder, "MainSalonView");
            House.MasterBedRoomViewPhotoUrl = await UploadedFiles.SaveAsync(MasterBedRoomViewPhoto, webRoot, folder, "MasterBedRoomView");
            House.KitchenViewPhotoUrl = await UploadedFiles.SaveAsync(KitchenViewPhoto, webRoot, folder, "KitchenView");
            House.RestRoomViewPhotoUrl = await UploadedFiles.SaveAsync(RestRoomViewPhoto, webRoot, folder, "RestRoomView");
            House.AuxiSalonViewPhotoUrl = await UploadedFiles.SaveAsync(AuxiSalonViewPhoto, webRoot, folder, "AuxiSalonView");
            House.SecBedRoomViewPhotoUrl = await UploadedFiles.SaveAsync(SecBedRoomViewPhoto, webRoot, folder, "SecBedRoomView");
            House.AdditionalViewPhotoUrl = await UploadedFiles.SaveAsync(AdditionalViewPhoto, webRoot, folder, "AdditionalView");
            House.BackyardViewPhotoUrl = await UploadedFiles.SaveAsync(BackyardViewPhoto, webRoot, folder, "BackyardView");
            House.Additional2PhotoUrl = await UploadedFiles.SaveAsync(Additional2Photo, webRoot, folder, "Additional2Photo");

            _context.Houses.Add(House);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                UploadedFiles.DeleteFolder(webRoot, folder); // don't leave the uploads of an unsaved house behind
                throw;
            }
            TempData["Success"] = "House created successfully!";
            return RedirectToPage("Index");
        }

        /// <summary>
        /// Sets <see cref="CanChooseOwner"/>; for non-admins also sets House.HouseOwnerId to their own customer profile.
        /// Returns false (and sets <see cref="ProfileMissing"/>) when a non-admin has no customer profile.
        /// </summary>
        private async Task<bool> ResolveOwnerAsync()
        {
            CanChooseOwner = HouseAccess.CanChooseOwner(User);
            if (CanChooseOwner)
            {
                return true;
            }

            var customerId = await HouseAccess.CurrentCustomerIdAsync(_userManager, User);
            var profile = customerId is Guid id
                ? await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id)
                : null;
            if (profile is null)
            {
                ProfileMissing = true;
                return false;
            }

            House.HouseOwnerId = profile.Id;
            OwnOwnerName = DisplayText.PersonName(profile);
            return true;
        }

        private void LoadRequiredData()
        {
            AddressList = HouseSelectLists.Addresses(_context, House.AddressId);
            ViewData["HouseCategoryId"] = HouseSelectLists.Categories(_context);
            if (CanChooseOwner)
            {
                ViewData["HouseOwnerId"] = HouseSelectLists.Customers(_context);
            }
        }
    }
}
