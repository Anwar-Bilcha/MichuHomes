using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Shared;

namespace RadiantHomes.Pages.Buildings
{
    public class DetailsModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }
        public List<string> Photos { get; set; } = new();
        public Building Building { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Building photos are ApartmentPhotos rows whose HouseId is the building's Id
            // (Building.Photos is configured with HouseId as its foreign key in ApplicationDbContext).
            var building = await _context.Buildings
                .Include(a=>a.Project).ThenInclude(a=>a.Company)
                .Include(a=>a.Photos)
                .Include(a=>a.Houses)
                .AsSplitQuery()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (building is not null)
            {
                Building = building;
                //var CompanyName = Building.Project.Company.Name;
                //Configurations.Initialize(_env);
                //var photosFolder = Path.Combine(Configurations.ApartmentHousePhotosFolder, CompanyName, $"{ApartmentHouse.Bedrooms}bed");
                //if (Directory.Exists(photosFolder))
                //{
                //    var files = Directory.GetFiles(photosFolder);
                //    Photos = files.Select(f =>
                //    {
                //        var relativePath = f.Substring(f.IndexOf("wwwroot") + "wwwroot".Length).Replace("\\", "/");
                //        return relativePath.StartsWith("/") ? relativePath : "/" + relativePath;
                //    }).ToList();
                //}
                return Page();
            }

            return NotFound();
        }
    }
}
