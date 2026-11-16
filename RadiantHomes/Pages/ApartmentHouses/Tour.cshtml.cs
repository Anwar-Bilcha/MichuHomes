using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.ApartmentHouses
{
    /// <summary>
    /// The rooms of an apartment's 3D walk-through: the owner walks through the model and saves named spots
    /// (see ApartmentModel3D.TourRoom, wwwroot/js/tour-editor.js). Company admins only (Program.cs), and only for
    /// their own company's apartments.
    /// </summary>
    public class TourModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public TourModel(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public ApartmentHouse Apartment { get; private set; } = default!;
        public ApartmentModel3D.Viewer Viewer { get; private set; } = default!;

        /// <summary>The rooms as JSON (posted back by the editor).</summary>
        [BindProperty]
        public string? RoomsJson { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            var problem = await LoadAsync(id);
            if (problem is not null) return problem;
            RoomsJson = ApartmentModel3D.RoomsToJson(Viewer.Rooms);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid? id)
        {
            var problem = await LoadAsync(id);
            if (problem is not null) return problem;

            var rooms = ApartmentModel3D.ParseRooms(RoomsJson, out var error);
            if (rooms is null)
            {
                ModelState.AddModelError(string.Empty, error!);
                return Page(); // RoomsJson keeps what was posted, so nothing is lost
            }

            ApartmentModel3D.SaveRooms(_env, Apartment.Id, rooms);
            TempData["Success"] = rooms.Count == 0
                ? "The 3D tour has no rooms now."
                : $"3D tour saved with {rooms.Count} room(s).";
            return RedirectToPage(new { id = Apartment.Id });
        }

        private async Task<IActionResult?> LoadAsync(Guid? id)
        {
            if (id is null) return NotFound();
            var apartment = await _context.ApartmentHouses.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (apartment is null) return NotFound();

            var scope = await CompanyScope.ForAsync(User, _context);
            if (!await scope.CanAccessHouseAsync(_context, apartment.Id)) return Forbid();

            var model = ApartmentModel3D.Find(_env, apartment.Id);
            if (model is null)
            {
                TempData["Warning"] = "Add a 3D model to the apartment first.";
                return RedirectToPage("./Edit", new { id = apartment.Id });
            }

            Apartment = apartment;
            var cover = ApartmentHouseForm.ListPhotos(_env, apartment.Id).Select(p => p.Url).FirstOrDefault();
            Viewer = new ApartmentModel3D.Viewer(model, apartment.Title, cover, ApartmentModel3D.LoadRooms(_env, apartment.Id), Editor: true);
            return null;
        }
    }
}
