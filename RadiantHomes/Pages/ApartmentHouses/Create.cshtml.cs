using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.ExtendedProperties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Services.NotificationService;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RadiantHomes.Pages.ApartmentHouses
{
    // A 3D model (up to 50 MB) plus photos can exceed Kestrel's default 30 MB request size.
    [RequestSizeLimit(ApartmentModel3D.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ApartmentModel3D.MaxRequestBytes)]
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notificationService;

        [BindProperty(SupportsGet = true)]
        public Guid BuildingId { get; set; }
        // Optional: nothing on this page posts it, so it must not be required when binding.
        [BindProperty(SupportsGet = true)]
        public string? RealEstateName { get; set; }
        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context, IWebHostEnvironment env, INotificationService notificationService)
        {
            _context = context;
            _env = env;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            _scope = await CompanyScope.ForAsync(User, _context);
            if (_scope.HasNoCompany) return NoCompanyRedirect();

            // New apartments are listed as available unless the form says otherwise.
            ApartmentHouse = new ApartmentHouse { IsAvailable = true };

            // Coming from a building ("Register houses"): preselect it (one of the user's own) and default to its address.
            if (id is Guid buildingId && buildingId != Guid.Empty)
            {
                var building = _scope.Buildings(_context.Buildings).AsNoTracking().FirstOrDefault(b => b.Id == buildingId);
                if (building != null)
                {
                    BuildingId = building.Id;
                    ApartmentHouse.BuildingId = building.Id;
                    ApartmentHouse.AddressId = building.AddressId;
                }
            }

            PopulateDropdowns();
            return Page();
        }
        //public IActionResult OnGetDownloadTemplate()
        //{
        //    var filePath = Path.Combine(_env.WebRootPath, "templates", "ModifiedApartmentHouseTemplate.xlsx");
        //    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        //    return PhysicalFile(filePath, contentType, "ApartmentHouseTemplate.xlsx");
        //}
        public IActionResult OnGetDownloadTemplate()
        {
            var filePath = Path.Combine(_env.WebRootPath, "templates", "ModifiedApartmenthouseTemplate.xlsx");

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Template file not found.");
            }
            var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                 stream.CopyTo(memory);
            }

            memory.Position = 0;

            return File(memory,contentType,"ApartmentHouseTemplate.xlsx");
        }
        [BindProperty]
        public IFormFile? ExcelFile { get; set; }

        public List<string> Errors { get; set; } = new();

        [BindProperty]
        public ApartmentHouse ApartmentHouse { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            _scope = await CompanyScope.ForAsync(User, _context);
            if (_scope.HasNoCompany) return NoCompanyRedirect();

            ModelState.Remove(nameof(ExcelFile));
            if (ApartmentHouse == null)
            {
                ApartmentHouse = new ApartmentHouse { IsAvailable = true };
                PopulateDropdowns();
                return Page();
            }

            // Navigations aren't posted; foreign keys, numbers and house type are checked instead.
            await ApartmentHouseForm.ValidateAsync(_context, ModelState, ApartmentHouse, _scope);
            var photoUploads = ApartmentHouseForm.CollectPhotoUploads(Request, ModelState);
            var modelUpload = await ApartmentModel3D.CollectAsync(Request, ModelState);

            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                return Page();
            }

            // CreatedOn/CreatedBy are stamped by ApplicationDbContext.SaveChanges.
            ApartmentHouse.Photos = new List<ApartmentPhoto>();
            ApartmentHouse.Id = Guid.NewGuid(); // The photos folder is named after the id.

            _context.ApartmentHouses.Add(ApartmentHouse);
            await _context.SaveChangesAsync();

            foreach (var (file, description) in photoUploads)
            {
                await ApartmentHouseForm.SavePhotoAsync(_env, ApartmentHouse.Id, file, description);
            }

            var modelNote = await ApartmentModel3D.ApplyAsync(_env, ApartmentHouse.Id, modelUpload, remove: false);

            var message = photoUploads.Count > 0
                ? $"Apartment \"{ApartmentHouse.Title}\" was created with {photoUploads.Count} photo(s) and is pending approval."
                : $"Apartment \"{ApartmentHouse.Title}\" was created and is pending approval.";
            TempData["Success"] = modelNote.Length > 0 ? $"{message} {modelNote}" : message;
            return RedirectToPage("./Details", new { id = ApartmentHouse.Id });
        }

        // Set by each handler: the user's company scope (a Real Estate Admin only works with their own buildings)
        private CompanyScope _scope = default!;

        private void PopulateDropdowns() => ApartmentHouseForm.PopulateDropdowns(_context, ViewData, ApartmentHouse, _scope);

        // A Real Estate Admin without a company: the company-admin apartment list explains why there is nothing to do.
        private IActionResult NoCompanyRedirect() => RedirectToPage("/ApartmentHouses/Index", new { area = "HousingCompanyAdmin" });

        // Bulk upload (Excel template): every row is validated first; a valid file becomes an upload batch of pending
        // apartments for the platform to review on Staff/Uploads (Services/Properties/BulkUploadService).
        public async Task<IActionResult> OnPostBulkExcelUploadAsync([FromServices] RadiantHomes.Services.Properties.BulkUploadService uploads)
        {
            _scope = await CompanyScope.ForAsync(User, _context);
            if (_scope.HasNoCompany) return NoCompanyRedirect();
            ApartmentHouse ??= new ApartmentHouse();

            var access = await RadiantHomes.Services.Security.AccessExtensions.AccessAsync(HttpContext);
            var result = await uploads.UploadAsync(_scope, access, User.Identity?.Name, BuildingId, ExcelFile);
            if (result.Succeeded)
            {
                TempData["Success"] = $"{result.Batch!.Rows.Count} apartments uploaded as batch {result.Batch.Reference} and sent for approval.";
                return RedirectToPage("/Staff/Uploads/Details", new { area = "", id = result.Batch.Id });
            }
            Errors.AddRange(result.Errors);
            PopulateDropdowns();
            return Page();
        }
    }
}
