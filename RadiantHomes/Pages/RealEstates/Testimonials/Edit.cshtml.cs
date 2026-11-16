using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.RealEstates.Testimonials
{
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context)
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

            var customertestimonial =  await _context.CustomerTestimonials.FirstOrDefaultAsync(m => m.Id == id);
            if (customertestimonial == null)
            {
                return NotFound();
            }
            // Real Estate Admins may only edit their own company's testimonials
            _scope = await CompanyScope.ForAsync(User, _context);
            if (!_scope.CanAccess(customertestimonial.CompanyId))
            {
                return Forbid();
            }
            CustomerTestimonial = customertestimonial;
            PopulateSelectLists();
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            _scope = await CompanyScope.ForAsync(User, _context);
            var storedCompanyId = await _context.CustomerTestimonials.AsNoTracking()
                .Where(t => t.Id == CustomerTestimonial.Id)
                .Select(t => (Guid?)t.CompanyId)
                .FirstOrDefaultAsync();
            if (storedCompanyId == null)
            {
                return NotFound();
            }
            if (!_scope.CanAccess(storedCompanyId))
            {
                return Forbid();
            }
            // ...and it stays with their own company, whatever company id is posted
            if (_scope.IsRestricted) CustomerTestimonial.CompanyId = _scope.CompanyId!.Value;

            // The navigations are never posted (only their ids), so their required errors don't apply.
            ModelState.Remove($"{nameof(CustomerTestimonial)}.{nameof(CustomerTestimonial.Company)}");
            ModelState.Remove($"{nameof(CustomerTestimonial)}.{nameof(CustomerTestimonial.Customer)}");
            if (!ModelState.IsValid)
            {
                PopulateSelectLists();
                return Page();
            }

            _context.Attach(CustomerTestimonial).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerTestimonialExists(CustomerTestimonial.Id))
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

        // Set by each handler: the user's company scope
        private CompanyScope _scope = default!;

        // Show names, not ids, in the company / customer dropdowns (a Real Estate Admin is only offered their own company).
        private void PopulateSelectLists()
        {
            ViewData["CompanyId"] = new SelectList(_scope.Companies(_context.HousingCompanies).OrderBy(c => c.Name).Select(c => new { c.CompanyId, c.Name }).ToList(), "CompanyId", "Name");
            ViewData["CustomerId"] = new SelectList(_context.UserProfiles.OrderBy(c => c.FirstName).ThenBy(c => c.LastName).Select(c => new { c.Id, Name = c.FirstName + " " + c.LastName }).ToList(), "Id", "Name");
        }

        private bool CustomerTestimonialExists(Guid id)
        {
            return _context.CustomerTestimonials.Any(e => e.Id == id);
        }
    }
}
