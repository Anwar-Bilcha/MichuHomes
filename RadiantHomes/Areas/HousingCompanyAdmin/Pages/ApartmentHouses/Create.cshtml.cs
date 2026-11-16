using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Pages.ApartmentHouses;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.ApartmentHouses
{
    // The optional 3D model can be up to 50 MB, above Kestrel's default 30 MB request size.
    [RequestSizeLimit(ApartmentModel3D.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ApartmentModel3D.MaxRequestBytes)]
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // id: optional building to preselect (Buildings/Edit links here with the building's id)
        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            _scope = await CompanyScope.ForAsync(User, _context);
            if (_scope.HasNoCompany) return RedirectToPage("./Index"); // the list explains why
            ApartmentHouse = new ApartmentHouse { HouseNumber = string.Empty, BuildingId = id ?? Guid.Empty };
            LoadSelectLists();
            return Page();
        }

        [BindProperty]
        public ApartmentHouse ApartmentHouse { get; set; } = default!;

        // The fields this form posts. The model's required navigation properties (ContactPerson, HouseCategory, …)
        // are never posted, so their validation errors are ignored instead of blocking every save.
        private static readonly string[] PostedFields =
        {
            nameof(ApartmentHouse.Title), nameof(ApartmentHouse.HouseNumber), nameof(ApartmentHouse.Description),
            nameof(ApartmentHouse.SalePrice), nameof(ApartmentHouse.HouseType), nameof(ApartmentHouse.FloorNumber),
            nameof(ApartmentHouse.AvailableFor), nameof(ApartmentHouse.HouseCategoryId), nameof(ApartmentHouse.AddressId),
            nameof(ApartmentHouse.BuildingId),
        };

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            foreach (var key in ModelState.Keys.Where(k => !PostedFields.Any(f => k == $"{nameof(ApartmentHouse)}.{f}")).ToList())
            {
                ModelState.Remove(key);
            }
            _scope = await CompanyScope.ForAsync(User, _context);
            if (_scope.HasNoCompany) return RedirectToPage("./Index");
            // Only one of the user's own buildings (any building for a Michu Admin)
            if (!await _scope.CanAccessBuildingAsync(_context, ApartmentHouse.BuildingId))
            {
                ModelState.Remove($"{nameof(ApartmentHouse)}.{nameof(ApartmentHouse.BuildingId)}");
                ModelState.AddModelError($"{nameof(ApartmentHouse)}.{nameof(ApartmentHouse.BuildingId)}", "Please choose one of your buildings.");
            }
            // Checked after the clean-up above, which would otherwise drop its error.
            var modelUpload = await ApartmentModel3D.CollectAsync(Request, ModelState);

            if (!ModelState.IsValid)
            {
                LoadSelectLists();
                return Page();
            }

            // A new house waits for approval (the public listing only shows approved houses).
            // Id and CreatedOn/CreatedBy are filled in by ApplicationDbContext.SaveChanges.
            ApartmentHouse.Status = "Pending";
            _context.ApartmentHouses.Add(ApartmentHouse);
            await _context.SaveChangesAsync();

            var modelNote = await ApartmentModel3D.ApplyAsync(_env, ApartmentHouse.Id, modelUpload, remove: false);
            var message = $"Apartment \"{ApartmentHouse.Title}\" was created and is pending approval.";
            TempData["Success"] = modelNote.Length > 0 ? $"{message} {modelNote}" : message;
            return RedirectToPage("./Index");
        }

        // Set by each handler: the user's company scope
        private CompanyScope _scope = default!;

        // Dropdowns show names, not ids (a Real Estate Admin is only offered their own company's buildings)
        private void LoadSelectLists()
        {
            ViewData["BuildingId"] = new SelectList(_scope.Buildings(_context.Buildings).OrderBy(b => b.BuildingName), "Id", "BuildingName");
            ViewData["AddressId"] = new SelectList(_context.Addresses.AsEnumerable().OrderBy(a => a.FormattedAddressCompact), "AddressID", "FormattedAddressCompact");
            ViewData["HouseCategoryId"] = new SelectList(_context.HouseCategories.OrderBy(c => c.Name), "Id", "Name");
        }
    }
}
