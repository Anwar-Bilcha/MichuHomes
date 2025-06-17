using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        public SelectList AddressList { get; set; }
        public CreateModel(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            House = new House();
        }

        public IActionResult OnGet()
        {
            AddressList = new SelectList(_context.Addresses.ToList(), "AddressID", "FormattedAddress");
            ViewData["HouseCategoryId"] = new SelectList(_context.HouseCategories, "Id", "Name");
            ViewData["HouseOwnerId"] = new SelectList(_context.UserProfiles, "Id", "FirstName");
            if (House.AvailableFor == "Rent" && House.HouseRentInfo == null)
            {
                House.HouseRentInfo = new RentInfo();
            }
            return Page();
        }

        [BindProperty] public IFormFile MainPhotoUrl { get; set; }
        [BindProperty] public IFormFile OutOfCompoundViewPhoto { get; set; }
        [BindProperty] public IFormFile FrontCompoundViewPhoto { get; set; }
        [BindProperty] public IFormFile HouseInternalViewPhoto { get; set; }
        [BindProperty] public IFormFile MainSalonViewPhoto { get; set; }
        [BindProperty] public IFormFile MasterBedRoomViewPhoto { get; set; }
        [BindProperty] public IFormFile KitchenViewPhoto { get; set; }
        [BindProperty] public IFormFile RestRoomViewPhoto { get; set; }
        [BindProperty] public IFormFile AuxiSalonViewPhoto { get; set; }
        [BindProperty] public IFormFile SecBedRoomViewPhoto { get; set; }
        [BindProperty] public IFormFile AdditionalViewPhoto { get; set; }
        [BindProperty] public IFormFile BackyardViewPhoto { get; set; }
        [BindProperty] public IFormFile Additional2Photo { get; set; }

        [BindProperty]
        public House House { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (House.AvailableFor == "Rent" && House.HouseRentInfo == null)
            {
                House.HouseRentInfo = new RentInfo();
            }
            //if (!ModelState.IsValid)
            //{
            //    LoadRequiredData(); // Reload dropdowns if validation fails
            //    return Page();
            //}
            House.CreatedBy = HttpContext.Session.GetString("CustomerId");
            if (House.AvailableFor == "Rent")
            {
                House.HouseRentInfo = new RentInfo
                {
                    HouseId = House.Id,
                    IsOnRent = false,
                    RentTerm = House.HouseRentInfo.RentTerm,
                    PricePerTerm = House.HouseRentInfo.PricePerTerm,
                    ExcepectedAdvancePayment = House.HouseRentInfo.ExcepectedAdvancePayment
                };
            }
            
            _context.Houses.Add(House);
            House.HouseHolderId = House.HouseOwnerId;
            await _context.SaveChangesAsync();
            var houseId = House.Id;
            var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "houses", houseId.ToString());

            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            // Handle Main Photo Upload
            if (MainPhotoUrl != null && MainPhotoUrl.Length > 0)
            {
                var fileName = $"MainPhoto{Path.GetExtension(MainPhotoUrl.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await MainPhotoUrl.CopyToAsync(stream);
                House.MainPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            // Handle other photo uploads with null checks
            if (FrontCompoundViewPhoto != null && FrontCompoundViewPhoto.Length > 0)
            {
                var fileName = $"FrontCompoundView{Path.GetExtension(FrontCompoundViewPhoto.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await FrontCompoundViewPhoto.CopyToAsync(stream);
                House.FrontCompoundViewPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            if (OutOfCompoundViewPhoto != null && OutOfCompoundViewPhoto.Length > 0)
            {
                var fileName = $"OutOfCompoundView{Path.GetExtension(OutOfCompoundViewPhoto.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await OutOfCompoundViewPhoto.CopyToAsync(stream);
                House.OutOfCompoundViewPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            if (HouseInternalViewPhoto != null && HouseInternalViewPhoto.Length > 0)
            {
                var fileName = $"HouseInternalView{Path.GetExtension(HouseInternalViewPhoto.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await HouseInternalViewPhoto.CopyToAsync(stream);
                House.HouseInternalViewPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            if (MainSalonViewPhoto != null && MainSalonViewPhoto.Length > 0)
            {
                var fileName = $"MainSalonView{Path.GetExtension(MainSalonViewPhoto.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await MainSalonViewPhoto.CopyToAsync(stream);
                House.MainSalonViewPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            if (MasterBedRoomViewPhoto != null && MasterBedRoomViewPhoto.Length > 0)
            {
                var fileName = $"MasterBedRoomView{Path.GetExtension(MasterBedRoomViewPhoto.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await MasterBedRoomViewPhoto.CopyToAsync(stream);
                House.MasterBedRoomViewPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            if (KitchenViewPhoto != null && KitchenViewPhoto.Length > 0)
            {
                var fileName = $"KitchenView{Path.GetExtension(KitchenViewPhoto.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await KitchenViewPhoto.CopyToAsync(stream);
                House.KitchenViewPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            if (RestRoomViewPhoto != null && RestRoomViewPhoto.Length > 0)
            {
                var fileName = $"RestRoomView{Path.GetExtension(RestRoomViewPhoto.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await RestRoomViewPhoto.CopyToAsync(stream);
                House.RestRoomViewPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            if (AuxiSalonViewPhoto != null && AuxiSalonViewPhoto.Length > 0)
            {
                var fileName = $"AuxiSalonView{Path.GetExtension(AuxiSalonViewPhoto.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await AuxiSalonViewPhoto.CopyToAsync(stream);
                House.AuxiSalonViewPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            if (SecBedRoomViewPhoto != null && SecBedRoomViewPhoto.Length > 0)
            {
                var fileName = $"SecBedRoomView{Path.GetExtension(SecBedRoomViewPhoto.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await SecBedRoomViewPhoto.CopyToAsync(stream);
                House.SecBedRoomViewPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            if (AdditionalViewPhoto != null && AdditionalViewPhoto.Length > 0)
            {
                var fileName = $"AdditionalView{Path.GetExtension(AdditionalViewPhoto.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await AdditionalViewPhoto.CopyToAsync(stream);
                House.AdditionalViewPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            if (BackyardViewPhoto != null && BackyardViewPhoto.Length > 0)
            {
                var fileName = $"BackyardView{Path.GetExtension(BackyardViewPhoto.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await BackyardViewPhoto.CopyToAsync(stream);
                House.BackyardViewPhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            if (Additional2Photo != null && Additional2Photo.Length > 0)
            {
                var fileName = $"Additional2Photo{Path.GetExtension(Additional2Photo.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await Additional2Photo.CopyToAsync(stream);
                House.Additional2PhotoUrl = $"/uploads/houses/{houseId}/{fileName}";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "House created successfully!";
            return RedirectToPage("Index");
        }

        private void LoadRequiredData()
        {
            ViewData["AddressId"] = new SelectList(_context.Addresses, "AddressID", "City");
            ViewData["HouseCategoryId"] = new SelectList(_context.HouseCategories, "Id", "Name");
            ViewData["HouseOwnerId"] = new SelectList(_context.UserProfiles, "Id", "FirstName");
        }
    }
}