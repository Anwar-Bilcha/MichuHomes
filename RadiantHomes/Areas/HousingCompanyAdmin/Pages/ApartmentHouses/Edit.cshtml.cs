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

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.ApartmentHouses
{
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ApartmentHouse ApartmentHouse { get; set; } = default!;

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
            ApartmentHouse = apartmenthouse;
           ViewData["BuildingId"] = new SelectList(_context.Buildings, "Id", "Id");
           ViewData["AddressId"] = new SelectList(_context.Addresses, "AddressID", "AddressID");
           ViewData["HouseCategoryId"] = new SelectList(_context.HouseCategories, "Id", "Id");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(ApartmentHouse).State = EntityState.Modified;

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

            return RedirectToPage("./Index");
        }

        private bool ApartmentHouseExists(Guid id)
        {
            return _context.ApartmentHouses.Any(e => e.Id == id);
        }
    }
}
