using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Pages.RealEstates.Testimonials
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
        ViewData["CompanyId"] = new SelectList(_context.HousingCompanies, "CompanyId", "CompanyId");
        ViewData["CustomerId"] = new SelectList(_context.UserProfiles, "Id", "Id");
            return Page();
        }

        [BindProperty]
        public CustomerTestimonial CustomerTestimonial { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            //if (!ModelState.IsValid)
            //{
            //    return Page();
            //}

            _context.CustomerTestimonials.Add(CustomerTestimonial);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
