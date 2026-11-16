using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Address
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            PartyAddress = new PartyAddress();
            return Page();
        }

        [BindProperty]
        public PartyAddress PartyAddress { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // City, Country and Street are required; without this check an empty form reached the
            // database and failed with a 500 (NOT NULL constraint) instead of showing the messages.
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Generate the key here: the model declares a NEWID() default for AddressID but the
            // database has no such default, so leaving it to SQL Server inserts NULL.
            PartyAddress.AddressID = Guid.NewGuid();
            _context.Addresses.Add(PartyAddress);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
