using System;
using System.IO;
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

        [BindProperty] public IFormFile MainPhotoUrl { get; set; }
        [BindProperty] public IFormFile OutOfCompoundViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile FrontCompoundViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile HouseInternalViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile MainSalonViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile MasterBedRoomViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile KitchenViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile RestRoomViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile AuxiSalonViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile SecBedRoomViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile AdditionalViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile BackyardViewPhotoUrl { get; set; }
        [BindProperty] public IFormFile Additional2PhotoUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null || _context.Houses == null)
            {
                return NotFound();
            }

            var house = await _context.Houses.Include(h=>h.HouseOwner).FirstOrDefaultAsync(h=>h.Id==id);
            var userId = _userManager.GetUserId(User);
            var customerId = _userManager.FindByIdAsync(userId).Result?.CustomerId; 

            // Block access if the user is not the owner and not an admin
            
            if (house == null)
            {
                return NotFound();
            }
            if (house.HouseOwnerId != customerId)
            {
                return Forbid();

            }
            House = house;
            ViewData["AddressId"] = new SelectList(_context.Addresses, "AddressID", "AddressID", House.AddressId);
            ViewData["HouseCategoryId"] = new SelectList(_context.HouseCategories, "Id", "Id", House.HouseCategoryId);
            ViewData["HouseHolderId"] = new SelectList(_context.Set<Customer>(), "Id", "Id", House.HouseHolderId);
            ViewData["HouseOwnerId"] = new SelectList(_context.UserProfiles, "Id", "Id", House.HouseOwnerId);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                ViewData["AddressId"] = new SelectList(_context.Addresses, "AddressID", "AddressID", House.AddressId);
                ViewData["HouseCategoryId"] = new SelectList(_context.HouseCategories, "Id", "Id", House.HouseCategoryId);
                ViewData["HouseHolderId"] = new SelectList(_context.Set<Customer>(), "Id", "Id", House.HouseHolderId);
                ViewData["HouseOwnerId"] = new SelectList(_context.UserProfiles, "Id", "Id", House.HouseOwnerId);
                return Page();
            }

            var houseToUpdate = await _context.Houses.FindAsync(House.Id);

            if (houseToUpdate == null)
            {
                return NotFound();
            }

            var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "houses", House.Id.ToString());

            // Handle Main Photo Update
            if (MainPhotoUrl != null && MainPhotoUrl.Length > 0)
            {
                if (!string.IsNullOrEmpty(houseToUpdate.MainPhotoUrl))
                {
                    var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, houseToUpdate.MainPhotoUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }
                var fileName = $"MainPhoto{Path.GetExtension(MainPhotoUrl.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);
                using var stream = new FileStream(filePath, FileMode.Create);
                await MainPhotoUrl.CopyToAsync(stream);
                houseToUpdate.MainPhotoUrl = $"/uploads/houses/{House.Id}/{fileName}";
            }

            // Handle other photo updates similarly (Out of Compound View)
            if (OutOfCompoundViewPhotoUrl != null && OutOfCompoundViewPhotoUrl.Length > 0)
            {
                if (!string.IsNullOrEmpty(houseToUpdate.OutOfCompoundViewPhotoUrl))
                {
                    var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, houseToUpdate.OutOfCompoundViewPhotoUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }
                var fileName = $"OutOfCompoundView{Path.GetExtension(OutOfCompoundViewPhotoUrl.FileName)}";
                var filePath = Path.Combine(uploadsPath, fileName);
                if (!Directory.Exists(uploadsPath)) Directory.CreateDirectory(uploadsPath);
                using var stream = new FileStream(filePath, FileMode.Create);
                await OutOfCompoundViewPhotoUrl.CopyToAsync(stream);
                houseToUpdate.OutOfCompoundViewPhotoUrl = $"/uploads/houses/{House.Id}/{fileName}";
            }

            // ... (Repeat the above logic for all other image properties) ...
            if (FrontCompoundViewPhotoUrl != null && FrontCompoundViewPhotoUrl.Length > 0) { /* ... */ }
            if (HouseInternalViewPhotoUrl != null && HouseInternalViewPhotoUrl.Length > 0) { /* ... */ }
            if (MainSalonViewPhotoUrl != null && MainSalonViewPhotoUrl.Length > 0) { /* ... */ }
            if (MasterBedRoomViewPhotoUrl != null && MasterBedRoomViewPhotoUrl.Length > 0) { /* ... */ }
            if (KitchenViewPhotoUrl != null && KitchenViewPhotoUrl.Length > 0) { /* ... */ }
            if (RestRoomViewPhotoUrl != null && RestRoomViewPhotoUrl.Length > 0) { /* ... */ }
            if (AuxiSalonViewPhotoUrl != null && AuxiSalonViewPhotoUrl.Length > 0) { /* ... */ }
            if (SecBedRoomViewPhotoUrl != null && SecBedRoomViewPhotoUrl.Length > 0) { /* ... */ }
            if (AdditionalViewPhotoUrl != null && AdditionalViewPhotoUrl.Length > 0) { /* ... */ }
            if (BackyardViewPhotoUrl != null && BackyardViewPhotoUrl.Length > 0) { /* ... */ }
            if (Additional2PhotoUrl != null && Additional2PhotoUrl.Length > 0) { /* ... */ }

            // Update other properties
            houseToUpdate.Title = House.Title;
            houseToUpdate.Description = House.Description;
            houseToUpdate.HouseOwnerId = House.HouseOwnerId;
            houseToUpdate.Price = House.Price;
            houseToUpdate.TotalBedRooms = House.TotalBedRooms;
            houseToUpdate.TotalRooms = House.TotalRooms;
            houseToUpdate.TotalArea = House.TotalArea;
            houseToUpdate.HasOwnershipDocument = House.HasOwnershipDocument;
            houseToUpdate.OwnershipDocumentUrl = House.OwnershipDocumentUrl; // Consider file upload for this too
            houseToUpdate.HouseCategoryId = House.HouseCategoryId;
            houseToUpdate.AvailableFor = House.AvailableFor;
            houseToUpdate.AddressId = House.AddressId;
            houseToUpdate.HouseOwnership = House.HouseOwnership;
            houseToUpdate.ConsentofSaleOrRentDocUrl = House.ConsentofSaleOrRentDocUrl; // Consider file upload
            houseToUpdate.LatestHouseTaxVoucherUrl = House.LatestHouseTaxVoucherUrl; // Consider file upload
            houseToUpdate.Status = House.Status;
            houseToUpdate.HouseHolderId = House.HouseHolderId;
            houseToUpdate.UpdatedBy = "CurrentUser"; // Replace with actual user
            houseToUpdate.UpdatedOn = DateTime.UtcNow;

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

        private bool HouseExists(Guid id)
        {
            return _context.Houses.Any(e => e.Id == id);
        }
    }
}