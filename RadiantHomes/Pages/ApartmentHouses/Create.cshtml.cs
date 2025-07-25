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
using RadiantHomes.Services.NotificationService;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace RadiantHomes.Pages.ApartmentHouses
{
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notificationService;

        [BindProperty(SupportsGet = true)]
        public Guid BuildingId { get; set; }
        [BindProperty(SupportsGet = true)]
        public string RealEstateName { get; set; }
        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context, IWebHostEnvironment env, INotificationService notificationService)
        {
            _context = context;
            _env = env;
            _notificationService = notificationService;
        }

        public IActionResult OnGet(Guid? id)
        {
           // ApartmentHouse = await _context.ApartmentHouses.Include(a => a.Building).ThenInclude(a => a.Project).ThenInclude(a => a.Company).FirstOrDefaultAsync(a => a.Id == id.Value);
            ApartmentHouse = new ApartmentHouse { Photos = new List<ApartmentPhoto> { new ApartmentPhoto() } };
            if(id == Guid.Empty || id ==null)
            {
                ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name");
                  return Page();
            }
            BuildingId = id.Value;
            ViewData["AddressId"] = new SelectList(_context.Addresses, "AddressID", "FormattedAddressCompact");
            ViewData["HouseCategoryId"] = new SelectList(_context.HouseCategories, "Id", "Name");
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
        public HousingCompany Company { get; set; }
        [BindProperty]
        public List<IFormFile> Photos { get; set; }

        [BindProperty]
        public List<string> Descriptions { get; set; }
        [BindProperty]
        public IFormFile ExcelFile { get; set; }

        public string? SuccessMessage { get; set; }
        public List<string> Errors { get; set; } = new();

        [BindProperty]
        public ApartmentHouse ApartmentHouse { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                return Page();
            }

            // Upload Main Photo
            

            // Upload Ownership Document
           
            ApartmentHouse.Photos = new List<ApartmentPhoto> { new ApartmentPhoto() };

            // Handle Apartment Photos
            var photoIndex = 0;
            while (Request.Form.Files[$"Photos[{photoIndex}].PhotoFile"] is IFormFile photo && Request.Form[$"Photos[{photoIndex}].Description"].Count > 0)
            {
                if (photo.Length > 0)
                {
                    var photoPath = await SaveFileAsync(photo, "apartmentphotos");

                    ApartmentHouse.Photos.Add(new ApartmentPhoto
                    {
                        PhotoPath = photoPath,
                         Name = Request.Form[$"Photos[{photoIndex}].Description"]
                    });
                }

                photoIndex++;
            }

            _context.ApartmentHouses.Add(ApartmentHouse);
            await _context.SaveChangesAsync();

            return RedirectToPage("Index");
        }

        private async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Path.Combine("/uploads", folder, fileName).Replace("\\", "/");
        }

        private void PopulateDropdowns()
        {
            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Name");
            ViewData["AddressId"] = new SelectList(_context.Addresses, "AddressID", "FormattedAddress");
            ViewData["HouseCategoryId"] = new SelectList(_context.HouseCategories, "Id", "Name");
        }

        public async Task<IActionResult> OnPostBulkExcelUploadAsync()
        {
            if (ExcelFile == null || ExcelFile.Length == 0)
            {
                Errors.Add("Please upload a valid Excel file.");
                return Page();
            }

            var apartmentHouses = new List<ApartmentHouse>();
            var houseCategory = _context.HouseCategories.FirstOrDefault(c => c.Name.ToLower() == "serviced apartment");
            var building = _context.Buildings.FirstOrDefault(a => a.Id == BuildingId) ?? new();
            var address = _context.Addresses.FirstOrDefault(a=>a.AddressID==building.AddressId);

            if (houseCategory == null)
            {
                Errors.Add("Apartment category not found in HouseCategories table.");
                return Page();
            }

            if (address == null)
            {
                Errors.Add("No address found in PartyAddresses table.");
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
                        Title = title,
                        HouseNumber = houseNumber,
                        Description = description,
                        SalePrice = salePrice,
                        AvailableFor = availableFor,
                        Status = status,
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
                SuccessMessage = $"Successfully uploaded {apartmentHouses.Count} apartment houses!";
            }

            return Page();
        }
    }
}
