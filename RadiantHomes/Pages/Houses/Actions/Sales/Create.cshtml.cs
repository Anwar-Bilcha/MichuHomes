using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Sales
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
        ViewData["OwnerId"] = new SelectList(_context.UserProfiles, "Id", "Id");
        ViewData["HouseId"] = new SelectList(_context.Houses, "Id", "Id");
        ViewData["CustomerId"] = new SelectList(_context.UserProfiles, "Id", "Id");
            return Page();
        }

        [BindProperty]
        public HouseSaleTransaction HouseSaleTransaction { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.HouseSaleTransactions.Add(HouseSaleTransaction);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
