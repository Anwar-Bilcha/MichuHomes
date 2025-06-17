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
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<CustomerTestimonial> CustomerTestimonial { get;set; } = default!;

        public async Task OnGetAsync()
        {
            CustomerTestimonial = await _context.CustomerTestimonials
                .Include(c => c.Company)
                .Include(c => c.Customer).ToListAsync();
        }
    }
}
