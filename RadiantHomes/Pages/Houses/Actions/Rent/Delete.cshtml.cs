using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context)
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

            var houserenttransaction = await _context.HouseRentTransactions.FirstOrDefaultAsync(m => m.Id == id);

            if (houserenttransaction is not null)
            {
                HouseRentTransaction = houserenttransaction;

                return Page();
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houserenttransaction = await _context.HouseRentTransactions.FindAsync(id);
            if (houserenttransaction != null)
            {
                HouseRentTransaction = houserenttransaction;
                _context.HouseRentTransactions.Remove(HouseRentTransaction);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
