using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses
{
    [Authorize]
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context, UserManager<AppUser> userManager, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public House House { get; set; } = default!;

        /// <summary>Why the house could not be deleted (shown above the confirmation).</summary>
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var house = await _context.Houses.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);

            if (house is null)
            {
                return NotFound();
            }
            // Same rule as Houses/Edit: only the owner or an admin.
            if (!await HouseAccess.CanManageAsync(_userManager, User, house))
            {
                return Forbid();
            }

            House = house;
            return Page();
        }

        // The id comes from the page URL (?id=…); it was bound as an int before, so the delete never ran.
        public async Task<IActionResult> OnPostAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var house = await _context.Houses
                .Include(h => h.HouseRentInfo)
                .FirstOrDefaultAsync(h => h.Id == id);
            if (house == null)
            {
                return RedirectToPage("./Index");
            }
            if (!await HouseAccess.CanManageAsync(_userManager, User, house))
            {
                return Forbid();
            }

            // The rent terms belong to the house; rent/sale transactions and complaints keep it (no cascade).
            if (house.HouseRentInfo is not null)
            {
                _context.HouseRentInfos.Remove(house.HouseRentInfo);
            }
            _context.Houses.Remove(house);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                House = await _context.Houses.AsNoTracking().FirstAsync(h => h.Id == id);
                ErrorMessage = "This house can't be deleted because rent, sale or complaint records refer to it.";
                return Page();
            }

            UploadedFiles.DeleteFolder(_webHostEnvironment.WebRootPath, $"uploads/houses/{house.Id}");
            TempData["Success"] = "House deleted.";
            return RedirectToPage("./Index");
        }
    }
}
