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

        public async Task<IActionResult> OnPostBulkExcelUploadAsync()
        {
            _scope = await CompanyScope.ForAsync(User, _context);
            if (_scope.HasNoCompany) return NoCompanyRedirect();

            ApartmentHouse ??= new ApartmentHouse();
            if (ExcelFile == null || ExcelFile.Length == 0)
            {
                Errors.Add("Please upload a valid Excel file.");
                PopulateDropdowns();
                return Page();
            }
            // The apartments go into one of the user's own buildings (any building for a Michu Admin)
            if (!await _scope.CanAccessBuildingAsync(_context, BuildingId))
            {
                Errors.Add("Please choose one of your buildings for the uploaded apartments.");
                PopulateDropdowns();
                return Page();
            }

            var apartmentHouses = new List<ApartmentHouse>();
            var houseCategory = _context.HouseCategories.FirstOrDefault(c => c.Name.ToLower() == "serviced apartment");
            var building = _context.Buildings.FirstOrDefault(a => a.Id == BuildingId) ?? new();
            var address = _context.Addresses.FirstOrDefault(a=>a.AddressID==building.AddressId);

            if (houseCategory == null)
            {
                Errors.Add("Apartment category not found in HouseCategories table.");
                PopulateDropdowns();
                return Page();
            }

            if (address == null)
            {
                Errors.Add("No address found in PartyAddresses table.");
                PopulateDropdowns();
                return Page();
            }

            using var stream = new MemoryStream();
            await ExcelFile.CopyToAsync(stream);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            int rowCount = worksheet.LastRowUsed().RowNumber();
            Notification notification = new Notification();
            for (int row = 2; row <= rowCount; row++)
            {
                var buildingId = BuildingId;
                try
                {
                    var title = worksheet.Cell(row, 1).GetString();
                    var houseNumber = worksheet.Cell(row, 2).GetString();
                    var description = worksheet.Cell(row, 3).GetString();
                    var salePrice = Convert.ToDouble(worksheet.Cell(row, 4).GetDouble());
                    var availableFor = worksheet.Cell(row, 5).GetString();
                    var status = worksheet.Cell(row, 6).GetString();
                    var floorNumber = worksheet.Cell(row, 7).GetValue<int>();
                    var bathrooms = worksheet.Cell(row, 8).GetValue<int>();
                    var totalRooms = worksheet.Cell(row, 9).GetValue<int>();
                    var isAvailable = worksheet.Cell(row, 10).GetBoolean();
                    var currency = worksheet.Cell(row, 11).GetString();
                    var livingRooms = worksheet.Cell(row, 12).GetValue<int>();
                    var bedrooms = worksheet.Cell(row, 13).GetValue<int>();
                    var hasBalcony = worksheet.Cell(row, 14).GetBoolean();
                    var hasElevator = worksheet.Cell(row, 15).GetBoolean();
                    var hasParking = worksheet.Cell(row, 16).GetBoolean();
                    var isFurnished = worksheet.Cell(row, 17).GetBoolean();
                    var rentPrice = worksheet.Cell(row, 18).GetDouble();
                    var mainPhoto = worksheet.Cell(row, 19).GetString();
                    var houseType = worksheet.Cell(row, 20).GetString();

                     building = await _context.Buildings.FindAsync(buildingId);
                    if (building == null)
                    {
                        Errors.Add($"Row {row}: Building ID not found.");
                        continue;
                    }

                    var apartment = new ApartmentHouse
                    {
                        Id = Guid.NewGuid(), // The model expects a NEWID() column default that the database no longer has, so the key is generated here.
                        Title = title,
                        HouseNumber = houseNumber,
                        Description = description,
                        SalePrice = salePrice,
                        AvailableFor = availableFor,
                        // Approval is a platform decision: only a Michu Admin's upload may mark houses approved.
                        Status = _scope.IsMichuAdmin || !ApartmentHouseApproval.IsApproved(status) ? status : "Pending",
                        BuildingId = buildingId,
                        Building = building,
                        FloorNumber = floorNumber,
                        Bathrooms = bathrooms,
                        TotalRooms = totalRooms,
                        IsAvailable = isAvailable,
                        Currency = currency,
                        LivingRooms = livingRooms,
                        Bedrooms = bedrooms,
                        HasBalcony = hasBalcony,
                        HasElevator = hasElevator,
                        HasParking = hasParking,
                        IsFurnished = isFurnished,
                        SalesStatus = "None",
                        // Custom Conditions
                        HouseType = HouseTypesCateg.Residential.ToString(),
                        AddressId = address.AddressID,
                        HouseAddress = address,
                        HouseCategoryId = houseCategory.Id,
                        HouseCategory = houseCategory,
                    };

                    apartmentHouses.Add(apartment);
                }
                catch (Exception ex)
                {
                    Errors.Add($"Row {row} error: {ex.Message}");
                }
            }
            Building BuildingFullInfo = _context.Buildings.Include(a => a.Project).ThenInclude(a => a.Company).ThenInclude(a=>a.ContactPerson).FirstOrDefault(a => a.Id == BuildingId) ?? new();
            var company = BuildingFullInfo?.Project?.Company;
            notification.Title = $"{apartmentHouses.Count} houses pending approval";
            notification.Message = $"{apartmentHouses.Count} Apartment Houses are registered for {company.Name}.";
            notification.RedirectUrl = "/ApartmentHouses";
            notification.ReceiverCompanyId = company.CompanyId;
            notification.ReceiverAdminId = company.ContactPerson.FirstOrDefault()?.Id !=Guid.Empty? company.ContactPerson.Select(a => a.Id).FirstOrDefault():company.CompanyId;
            notification.RecipientRole = MichuHomesRoles.RealEstateAdmin;
            notification.Datatype = EntityAffecteds.ApartmentHouses.ToString();

            if (!Errors.Any())
            {
               await _context.ApartmentHouses.AddRangeAsync(apartmentHouses);
                
                await _context.SaveChangesAsync();
                List<Guid> houseIds = apartmentHouses.Select(a => a.Id).ToList();

                foreach (var id in houseIds)
                {
                    notification.DataKey = notification.DataKey + id + ", ";
                }
              var result=  await _notificationService.CreateNotificationAsync(notification);
                var norificationWithRedirectUrl = _context.Notifications.FirstOrDefault(a => a.Id == notification.Id);
                if (norificationWithRedirectUrl != null)
                {
                    norificationWithRedirectUrl.RedirectUrl = $"/Admin/Notifications/NotificationDetails?notificationId={notification.Id}";
                    await _context.SaveChangesAsync();
                }
               
                    //await _notificationService.SendToAdminsAsync($"{apartmentHouses.Count} houses pending approval", $"{apartmentHouses.Count} Apartment Houses are registered for {company}.", "/ApartmentHouses");
                // Post-redirect-get: the toast is shown on the fresh form (_FlashMessages).
                TempData["Success"] = $"{apartmentHouses.Count} apartment houses uploaded and sent for approval.";
                return RedirectToPage();
            }

            PopulateDropdowns();
            return Page();
        }
    }
}
