using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Rent
{
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public HouseRentTransaction HouseRentTransaction { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houserenttransaction =  await _context.HouseRentTransactions.FirstOrDefaultAsync(m => m.Id == id);
            if (houserenttransaction == null)
            {
                return NotFound();
            }
            HouseRentTransaction = houserenttransaction;
           ViewData["OwnerId"] = new SelectList(_context.UserProfiles, "Id", "Id");
           ViewData["HouseId"] = new SelectList(_context.Houses, "Id", "Id");
           ViewData["CustomerId"] = new SelectList(_context.UserProfiles, "Id", "Id");
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

            _context.Attach(HouseRentTransaction).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HouseRentTransactionExists(HouseRentTransaction.Id))
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

        private bool HouseRentTransactionExists(Guid id)
        {
            return _context.HouseRentTransactions.Any(e => e.Id == id);
        }
    }
}
