using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.RealEstates.Testimonials
{
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            _scope = await CompanyScope.ForAsync(User, _context);
            if (_scope.HasNoCompany) return RedirectToPage("./Index"); // the list explains why
            CustomerTestimonial = new CustomerTestimonial { SubmissionDate = DateTime.Today, CompanyId = _scope.CompanyId ?? Guid.Empty };
            PopulateSelectLists();
            return Page();
        }

        [BindProperty]
        public CustomerTestimonial CustomerTestimonial { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            _scope = await CompanyScope.ForAsync(User, _context);
            if (_scope.HasNoCompany) return RedirectToPage("./Index");
            // A Real Estate Admin's testimonial always belongs to their own company, whatever company id is posted
            if (_scope.IsRestricted) CustomerTestimonial.CompanyId = _scope.CompanyId!.Value;

            // The navigations are never posted (only their ids), so their required errors don't apply.
            ModelState.Remove($"{nameof(CustomerTestimonial)}.{nameof(CustomerTestimonial.Company)}");
            ModelState.Remove($"{nameof(CustomerTestimonial)}.{nameof(CustomerTestimonial.Customer)}");
            // Empty dropdown choices are binding errors ("The value '' is invalid."); replaced by existence checks.
            ModelState.Remove($"{nameof(CustomerTestimonial)}.{nameof(CustomerTestimonial.CustomerId)}");
            ModelState.Remove($"{nameof(CustomerTestimonial)}.{nameof(CustomerTestimonial.CompanyId)}");
            if (!_context.UserProfiles.Any(c => c.Id == CustomerTestimonial.CustomerId))
                ModelState.AddModelError($"{nameof(CustomerTestimonial)}.{nameof(CustomerTestimonial.CustomerId)}", "Please select a customer.");
            if (!_context.HousingCompanies.Any(c => c.CompanyId == CustomerTestimonial.CompanyId))
                ModelState.AddModelError($"{nameof(CustomerTestimonial)}.{nameof(CustomerTestimonial.CompanyId)}", "Please select a company.");
            if (ModelState.GetFieldValidationState($"{nameof(CustomerTestimonial)}.{nameof(CustomerTestimonial.Rating)}") != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid
                && (CustomerTestimonial.Rating < 1 || CustomerTestimonial.Rating > 5))
                ModelState.AddModelError($"{nameof(CustomerTestimonial)}.{nameof(CustomerTestimonial.Rating)}", "Rating must be between 1 and 5.");
            if (!ModelState.IsValid)
            {
                PopulateSelectLists();
                return Page();
            }

            // Default the submission date when the form leaves it empty; CreatedOn/CreatedBy are stamped by ApplicationDbContext.
            if (CustomerTestimonial.SubmissionDate == default) CustomerTestimonial.SubmissionDate = DateTime.Now;

            CustomerTestimonial.Id = Guid.NewGuid();
            _context.CustomerTestimonials.Add(CustomerTestimonial);
            await _context.SaveChangesAsync();

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
    }
}
