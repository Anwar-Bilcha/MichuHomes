using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Pages.ApartmentHouses
{
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

        [BindProperty]
        public IFormFile? MainPhotoFile { get; set; }

        [BindProperty]
        public List<IFormFile>? PhotoGallery { get; set; }
        public Guid CompanyId { get; set; }
        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
                return NotFound();

            ApartmentHouse = await _context.ApartmentHouses.Include(a=>a.Building).ThenInclude(a=>a.Project).ThenInclude(a=>a.Company)
                .Include(a => a.Photos) // Assuming navigation property for multiple photos
                .FirstOrDefaultAsync(m => m.Id == id);
            if(ApartmentHouse !=null)
            {
                CompanyId = ApartmentHouse.Building.Project.HousingCompanyId;
            }
            CompanyId = CompanyId;
            if (ApartmentHouse == null)
                return NotFound();

            ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Id");
            ViewData["AddressId"] = new SelectList(_context.Addresses, "AddressID", "AddressID");
            ViewData["HouseCategoryId"] = new SelectList(_context.HouseCategories, "Id", "Id");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            //if (!ModelState.IsValid)
            //    return Page();

            var existingHouse = await _context.ApartmentHouses
                .Include(a => a.Photos)
                .FirstOrDefaultAsync(h => h.Id == ApartmentHouse.Id);

            if (existingHouse == null)
                return NotFound();

            // Update fields manually (or use AutoMapper if preferred)
            ApartmentHouse.BuildingId = ApartmentHouse.BuildingId;

            _context.Entry(existingHouse).CurrentValues.SetValues(ApartmentHouse);
            ApartmentHouse.BuildingId = ApartmentHouse.BuildingId;
            // Handle Main Photo
            

            // Handle Multiple Photos Upload
            if (PhotoGallery != null && PhotoGallery.Any())
            {
                foreach (var photo in PhotoGallery)
                {
                    var photoPath = await SaveFileAsync(photo, "uploads/apartmenthouses/gallery");

                    existingHouse.Photos.Add(new ApartmentPhoto
                    {
                        HouseId = ApartmentHouse.Id,
                        Name = "aaa",
                        PhotoPath = photoPath,
                    });
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ApartmentHouseExists(ApartmentHouse.Id))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToPage("./Index");
        }

        private async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            var uploadsRoot = Path.Combine(_env.WebRootPath, folder);
            if (!Directory.Exists(uploadsRoot))
                Directory.CreateDirectory(uploadsRoot);

            var uniqueFileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsRoot, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return "/" + folder.Replace("\\", "/") + "/" + uniqueFileName;
        }

        private bool ApartmentHouseExists(Guid id)
        {
            return _context.ApartmentHouses.Any(e => e.Id == id);
        }
    }
}
