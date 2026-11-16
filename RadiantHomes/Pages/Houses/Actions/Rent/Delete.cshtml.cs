using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Rent
{
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public HouseRentTransaction HouseRentTransaction { get; set; } = default!;

        /// <summary>Why the record could not be deleted (shown above the confirmation).</summary>
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houserenttransaction = await _context.HouseRentTransactions
                .AsNoTracking()
                .Include(t => t.House)
                .Include(t => t.Receiver)
                .Include(t => t.HomeOwner)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (houserenttransaction is null)
            {
                return NotFound();
            }
            if (!await CanAccessAsync(houserenttransaction))
            {
                return Forbid();
            }

            HouseRentTransaction = houserenttransaction;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houserenttransaction = await _context.HouseRentTransactions
                .FirstOrDefaultAsync(m => m.Id == id);
            if (houserenttransaction == null)
            {
                return RedirectToPage("./Index");
            }
            if (!await CanAccessAsync(houserenttransaction))
            {
                return Forbid();
            }

            _context.HouseRentTransactions.Remove(houserenttransaction);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                HouseRentTransaction = await _context.HouseRentTransactions
                    .AsNoTracking()
                    .Include(t => t.House)
                    .Include(t => t.Receiver)
                    .Include(t => t.HomeOwner)
                    .FirstOrDefaultAsync(m => m.Id == id) ?? houserenttransaction;
                ErrorMessage = "This rent transaction can't be deleted because a payment refers to it.";
                return Page();
            }

            TempData["Success"] = "Rent transaction deleted.";
            return RedirectToPage("./Index");
        }

        /// <summary>A Michu admin or a party to the rent transaction (owner / tenant).</summary>
        private Task<bool> CanAccessAsync(HouseRentTransaction record) =>
            TransactionAccess.CanAccessAsync(_userManager, User, record.OwnerId, record.CustomerId);
    }
}
