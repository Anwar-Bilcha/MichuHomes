using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.ApartmentHouses
{
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
        ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Id");
        ViewData["AddressId"] = new SelectList(_context.Addresses, "AddressID", "AddressID");
        ViewData["HouseCategoryId"] = new SelectList(_context.HouseCategories, "Id", "Id");
            return Page();
        }

        [BindProperty]
        public ApartmentHouse ApartmentHouse { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.ApartmentHouses.Add(ApartmentHouse);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
