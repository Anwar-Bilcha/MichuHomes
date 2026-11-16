using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Pages.ApartmentHouses;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.ApartmentHouses
{
    // The optional 3D model can be up to 50 MB, above Kestrel's default 30 MB request size.
    [RequestSizeLimit(ApartmentModel3D.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ApartmentModel3D.MaxRequestBytes)]
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [BindProperty]
        public ApartmentHouse ApartmentHouse { get; set; } = default!;

        /// <summary>The 3D model field: the apartment's model, if it has one, and its tour rooms (see ApartmentModel3D).</summary>
        public ApartmentModel3D.Field Model3D { get; set; } = new(null);

        // Status is also the approval gate (the public listing shows only "Approved" houses), and approving is a
        // platform decision: only a Michu Admin may change it here. Others see it read-only; a posted value is ignored.
        public bool CanEditStatus => User.IsInRole(MichuHomesRoles.MichuAdmin);

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var apartmenthouse =  await _context.ApartmentHouses.FirstOrDefaultAsync(m => m.Id == id);
            if (apartmenthouse == null)
            {
                return NotFound();
            }
            // Real Estate Admins may only edit their own company's apartments
            _scope = await CompanyScope.ForAsync(User, _context);
            if (!await _scope.CanAccessHouseAsync(_context, apartmenthouse.Id))
            {
                return Forbid();
            }
            ApartmentHouse = apartmenthouse;
            LoadSelectLists();
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        // The fields this form edits. Everything else on the house (price, rooms, contact person, photos…)
        // is not on the form, so it is kept from the database instead of being overwritten with defaults.
        private static readonly string[] EditableFields =
        {
            nameof(ApartmentHouse.Title), nameof(ApartmentHouse.HouseNumber), nameof(ApartmentHouse.Description),
            nameof(ApartmentHouse.HouseCategoryId), nameof(ApartmentHouse.AvailableFor), nameof(ApartmentHouse.AddressId),
            nameof(ApartmentHouse.Status), nameof(ApartmentHouse.HouseType), nameof(ApartmentHouse.FloorNumber),
            nameof(ApartmentHouse.BuildingId),
        };

        public async Task<IActionResult> OnPostAsync()
        {
            var existing = await _context.ApartmentHouses.FirstOrDefaultAsync(a => a.Id == ApartmentHouse.Id);
            if (existing == null)
            {
                return NotFound();
            }
            _scope = await CompanyScope.ForAsync(User, _context);
            if (!await _scope.CanAccessHouseAsync(_context, existing.Id))
            {
                return Forbid();
            }
            // ...and the apartment can only be moved to one of the user's own buildings
            if (!await _scope.CanAccessBuildingAsync(_context, ApartmentHouse.BuildingId))
            {
                ModelState.AddModelError($"{nameof(ApartmentHouse)}.{nameof(ApartmentHouse.BuildingId)}", "Please choose one of your buildings.");
            }

            // Only validate what the form posts (required navigation properties such as ContactPerson,
            // HouseCategory and HouseAddress are never posted and made every save fail silently)
            foreach (var key in ModelState.Keys.Where(k => !EditableFields.Any(f => k == $"{nameof(ApartmentHouse)}.{f}")).ToList())
            {
                ModelState.Remove(key);
            }
            if (!CanEditStatus)
            {
                ModelState.Remove($"{nameof(ApartmentHouse)}.{nameof(ApartmentHouse.Status)}");
                ApartmentHouse.Status = existing.Status; // also what the form shows again if validation fails
            }
            // Checked after the clean-up above, which would otherwise drop its error.
            var modelUpload = await ApartmentModel3D.CollectAsync(Request, ModelState);
            if (!ModelState.IsValid)
            {
                LoadSelectLists();
                return Page();
            }

            existing.Title = ApartmentHouse.Title;
            existing.HouseNumber = ApartmentHouse.HouseNumber;
            existing.Description = ApartmentHouse.Description;
            existing.HouseCategoryId = ApartmentHouse.HouseCategoryId;
            existing.AvailableFor = ApartmentHouse.AvailableFor;
            existing.AddressId = ApartmentHouse.AddressId;
            if (CanEditStatus) existing.Status = ApartmentHouse.Status;
            existing.HouseType = ApartmentHouse.HouseType;
            existing.FloorNumber = ApartmentHouse.FloorNumber;
            existing.BuildingId = ApartmentHouse.BuildingId; // UpdatedOn/UpdatedBy are stamped by ApplicationDbContext.SaveChanges

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ApartmentHouseExists(ApartmentHouse.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            var modelNote = await ApartmentModel3D.ApplyAsync(_env, existing.Id, modelUpload, ApartmentModel3D.RemoveRequested(Request));
            TempData["Success"] = modelNote.Length > 0
                ? $"Apartment \"{existing.Title}\" was saved. {modelNote}"
                : $"Apartment \"{existing.Title}\" was saved.";
            return RedirectToPage("./Index");
        }

        // Dropdowns show names, not ids
        // Set by each handler: the user's company scope
        private CompanyScope _scope = default!;

        private void LoadSelectLists()
        {
            ViewData["BuildingId"] = new SelectList(_scope.Buildings(_context.Buildings).OrderBy(b => b.BuildingName), "Id", "BuildingName");
            ViewData["AddressId"] = new SelectList(_context.Addresses.AsEnumerable().OrderBy(a => a.FormattedAddressCompact), "AddressID", "FormattedAddressCompact");
            ViewData["HouseCategoryId"] = new SelectList(_context.HouseCategories.OrderBy(c => c.Name), "Id", "Name");
            Model3D = new(ApartmentModel3D.Find(_env, ApartmentHouse.Id), ApartmentHouse.Id,
                          ApartmentModel3D.LoadRooms(_env, ApartmentHouse.Id).Count);
        }

        private bool ApartmentHouseExists(Guid id)
        {
            return _context.ApartmentHouses.Any(e => e.Id == id);
        }
    }
}
