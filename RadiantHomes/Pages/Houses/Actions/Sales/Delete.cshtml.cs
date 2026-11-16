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

namespace RadiantHomes.Pages.Houses.Actions.Sales
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
        public HouseSaleTransaction HouseSaleTransaction { get; set; } = default!;

        /// <summary>Why the record could not be deleted (shown above the confirmation).</summary>
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var housesaletransaction = await _context.HouseSaleTransactions
                .AsNoTracking()
                .Include(t => t.House)
                .Include(t => t.Receiver)
                .Include(t => t.HomeOwner)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (housesaletransaction is null)
            {
                return NotFound();
            }
            if (!await CanAccessAsync(housesaletransaction))
            {
                return Forbid();
            }

            HouseSaleTransaction = housesaletransaction;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var housesaletransaction = await _context.HouseSaleTransactions
                .FirstOrDefaultAsync(m => m.Id == id);
            if (housesaletransaction == null)
            {
                return RedirectToPage("./Index");
            }
            if (!await CanAccessAsync(housesaletransaction))
            {
                return Forbid();
            }

            _context.HouseSaleTransactions.Remove(housesaletransaction);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                HouseSaleTransaction = await _context.HouseSaleTransactions
                    .AsNoTracking()
                    .Include(t => t.House)
                    .Include(t => t.Receiver)
                    .Include(t => t.HomeOwner)
                    .FirstOrDefaultAsync(m => m.Id == id) ?? housesaletransaction;
                ErrorMessage = "This house sale can't be deleted because a payment refers to it.";
                return Page();
            }

            TempData["Success"] = "House sale deleted.";
            return RedirectToPage("./Index");
        }

        /// <summary>A Michu admin or a party to the house sale (seller / buyer).</summary>
        private Task<bool> CanAccessAsync(HouseSaleTransaction record) =>
            TransactionAccess.CanAccessAsync(_userManager, User, record.OwnerId, record.CustomerId);
    }
}
