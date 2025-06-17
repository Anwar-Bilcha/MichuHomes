using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Pages.RealEstates.Testimonials
{
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public CustomerTestimonial CustomerTestimonial { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customertestimonial = await _context.CustomerTestimonials.FirstOrDefaultAsync(m => m.Id == id);

            if (customertestimonial is not null)
            {
                CustomerTestimonial = customertestimonial;

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

            var customertestimonial = await _context.CustomerTestimonials.FindAsync(id);
            if (customertestimonial != null)
            {
                CustomerTestimonial = customertestimonial;
                _context.CustomerTestimonials.Remove(CustomerTestimonial);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
