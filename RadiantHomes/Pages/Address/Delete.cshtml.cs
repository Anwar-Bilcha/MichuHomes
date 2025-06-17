using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Address
{
    [Authorize]
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public PartyAddress PartyAddress { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var partyaddress = await _context.Addresses.FirstOrDefaultAsync(m => m.AddressID == id);

            if (partyaddress is not null)
            {
                PartyAddress = partyaddress;

                return Page();
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var partyaddress = await _context.Addresses.FindAsync(id);
            if (partyaddress != null)
            {
                PartyAddress = partyaddress;
                _context.Addresses.Remove(PartyAddress);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
