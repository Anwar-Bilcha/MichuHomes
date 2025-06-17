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

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.Financial
{
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public CompanyFinancialDetail CompanyFinancialDetail { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var companyfinancialdetail =  await _context.HousingCompanyFinancialDetails.FirstOrDefaultAsync(m => m.Id == id);
            if (companyfinancialdetail == null)
            {
                return NotFound();
            }
            CompanyFinancialDetail = companyfinancialdetail;
           ViewData["CompanyId"] = new SelectList(_context.HousingCompanies, "CompanyId", "CompanyId");
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

            _context.Attach(CompanyFinancialDetail).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CompanyFinancialDetailExists(CompanyFinancialDetail.Id))
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

        private bool CompanyFinancialDetailExists(Guid id)
        {
            return _context.HousingCompanyFinancialDetails.Any(e => e.Id == id);
        }
    }
}
