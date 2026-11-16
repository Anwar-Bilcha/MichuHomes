using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Pages.ApartmentHouses;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.ApartmentHouses
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

        public ApartmentHouse ApartmentHouse { get; set; } = default!;

        /// <summary>The "3D live view" card, when the apartment has a 3D model (see ApartmentModel3D).</summary>
        public ApartmentModel3D.Viewer? Model3D { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var apartmenthouse = await _context.ApartmentHouses
                .Include(m => m.HouseCategory)
                .Include(m => m.HouseAddress)
                .Include(m => m.Building).ThenInclude(b => b!.Project).ThenInclude(p => p!.Company)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (apartmenthouse is not null)
            {
                // Real Estate Admins may only open their own company's records
                if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(apartmenthouse.Building?.Project?.HousingCompanyId))
                {
                    return Forbid();
                }
                ApartmentHouse = apartmenthouse;
                if (ApartmentModel3D.Find(_env, apartmenthouse.Id) is { } model3D)
                {
                    var cover = ApartmentHouseForm.ListPhotos(_env, apartmenthouse.Id).Select(p => p.Url).FirstOrDefault();
                    Model3D = new ApartmentModel3D.Viewer(model3D, apartmenthouse.Title, cover,
                                                          ApartmentModel3D.LoadRooms(_env, apartmenthouse.Id));
                }

                return Page();
            }

            return NotFound();
        }
    }
}
