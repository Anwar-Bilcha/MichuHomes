using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.ApartmentHouses
{
    // A 3D model (up to 50 MB) plus photos can exceed Kestrel's default 30 MB request size.
    [RequestSizeLimit(ApartmentModel3D.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ApartmentModel3D.MaxRequestBytes)]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public EditModel(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [BindProperty]
        public ApartmentHouse ApartmentHouse { get; set; } = default!;

        /// <summary>File names of the apartment's own photos ticked for removal.</summary>
        [BindProperty]
        public List<string> RemovePhotos { get; set; } = new();

        /// <summary>The apartment's own photos (wwwroot/uploads/apartmentphotos/{id}/).</summary>
        public List<(string FileName, string Url)> Photos { get; set; } = new();

        /// <summary>The 3D model field: the apartment's model, if it has one, and its tour rooms (see ApartmentModel3D).</summary>
        public ApartmentModel3D.Field Model3D { get; set; } = new(null);

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
                return NotFound();

            var apartment = await _context.ApartmentHouses.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (apartment == null)
                return NotFound();
            // Real Estate Admins may only edit their own company's apartments
            _scope = await CompanyScope.ForAsync(User, _context);
            if (!await _scope.CanAccessHouseAsync(_context, apartment.Id))
                return Forbid();

            ApartmentHouse = apartment;
            LoadPageData();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ApartmentHouse == null)
                return BadRequest();

            var existingHouse = await _context.ApartmentHouses.FirstOrDefaultAsync(h => h.Id == ApartmentHouse.Id);
            if (existingHouse == null)
                return NotFound();
            _scope = await CompanyScope.ForAsync(User, _context);
            if (!await _scope.CanAccessHouseAsync(_context, existingHouse.Id))
                return Forbid();

            // The building can only be changed to one of the user's own buildings
            await ApartmentHouseForm.ValidateAsync(_context, ModelState, ApartmentHouse, _scope);
            var photoUploads = ApartmentHouseForm.CollectPhotoUploads(Request, ModelState);
            var modelUpload = await ApartmentModel3D.CollectAsync(Request, ModelState);

            if (!ModelState.IsValid)
            {
                LoadPageData();
                return Page();
            }

            // Copy only what the form edits: status, sales status, contact person and the audit fields are kept
            // (a blanket SetValues would reset them to the model defaults, e.g. an approved apartment to "Pending").
            existingHouse.Title = ApartmentHouse.Title;
            existingHouse.HouseNumber = ApartmentHouse.HouseNumber;
            existingHouse.Description = ApartmentHouse.Description;
            existingHouse.SalePrice = ApartmentHouse.SalePrice;
            existingHouse.Currency = ApartmentHouse.Currency;
            existingHouse.BuildingId = ApartmentHouse.BuildingId;
            existingHouse.HouseCategoryId = ApartmentHouse.HouseCategoryId;
            existingHouse.AddressId = ApartmentHouse.AddressId;
            existingHouse.HouseType = ApartmentHouse.HouseType;
            existingHouse.AvailableFor = ApartmentHouse.AvailableFor;
            existingHouse.FloorNumber = ApartmentHouse.FloorNumber;
            existingHouse.Bedrooms = ApartmentHouse.Bedrooms;
            existingHouse.TotalRooms = ApartmentHouse.TotalRooms;
            existingHouse.Bathrooms = ApartmentHouse.Bathrooms;
            existingHouse.LivingRooms = ApartmentHouse.LivingRooms;
            existingHouse.Area = ApartmentHouse.Area;
            existingHouse.IsAvailable = ApartmentHouse.IsAvailable;
            existingHouse.HasParking = ApartmentHouse.HasParking;
            existingHouse.HasBalcony = ApartmentHouse.HasBalcony;
            existingHouse.HasElevator = ApartmentHouse.HasElevator;
            existingHouse.IsFurnished = ApartmentHouse.IsFurnished;

            await _context.SaveChangesAsync(); // UpdatedOn/UpdatedBy are stamped by ApplicationDbContext

            // Photos are files on disk (see ApartmentHouseForm), handled once the record is saved.
            var removed = RemovePhotos.Distinct().Count(name => ApartmentHouseForm.DeletePhoto(_env, existingHouse.Id, name));
            foreach (var (file, description) in photoUploads)
            {
                await ApartmentHouseForm.SavePhotoAsync(_env, existingHouse.Id, file, description);
            }

            var photoNote = (photoUploads.Count, removed) switch
            {
                (0, 0) => string.Empty,
                (var added, 0) => $" {added} photo(s) added.",
                (0, var gone) => $" {gone} photo(s) removed.",
                (var added, var gone) => $" {added} photo(s) added, {gone} removed.",
            };
            var modelNote = await ApartmentModel3D.ApplyAsync(_env, existingHouse.Id, modelUpload, ApartmentModel3D.RemoveRequested(Request));
            if (modelNote.Length > 0) modelNote = " " + modelNote;
            TempData["Success"] = $"Apartment \"{existingHouse.Title}\" was saved.{photoNote}{modelNote}";
            return RedirectToPage("./Details", new { id = existingHouse.Id });
        }

        // Set by each handler: the user's company scope
        private CompanyScope _scope = default!;

        private void LoadPageData()
        {
            ApartmentHouseForm.PopulateDropdowns(_context, ViewData, ApartmentHouse, _scope);
            Photos = ApartmentHouseForm.ListPhotos(_env, ApartmentHouse.Id);
            Model3D = new(ApartmentModel3D.Find(_env, ApartmentHouse.Id), ApartmentHouse.Id,
                          ApartmentModel3D.LoadRooms(_env, ApartmentHouse.Id).Count);
        }
    }
}
