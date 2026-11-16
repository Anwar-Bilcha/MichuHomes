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
            // Real Estate Admins may only edit their own company's financial details
            _scope = await CompanyScope.ForAsync(User, _context);
            if (!_scope.CanAccess(companyfinancialdetail.CompanyId))
            {
                return Forbid();
            }
            CompanyFinancialDetail = companyfinancialdetail;
            LoadSelectLists();
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            _scope = await CompanyScope.ForAsync(User, _context);
            var storedCompanyId = await _context.HousingCompanyFinancialDetails.AsNoTracking()
                .Where(f => f.Id == CompanyFinancialDetail.Id)
                .Select(f => (Guid?)f.CompanyId)
                .FirstOrDefaultAsync();
            if (storedCompanyId == null)
            {
                return NotFound();
            }
            if (!_scope.CanAccess(storedCompanyId))
            {
                return Forbid();
            }
            // ...and the record can't be moved to another company
            if (!_scope.CanAccess(CompanyFinancialDetail.CompanyId))
            {
                ModelState.AddModelError("CompanyFinancialDetail.CompanyId", "Please choose your own company.");
            }

            // The statement URL only matters when an audited statement exists
            if (!CompanyFinancialDetail.hasAuditedFinancialStatement && string.IsNullOrWhiteSpace(CompanyFinancialDetail.AuditedFinancialStatementUrl))
            {
                CompanyFinancialDetail.AuditedFinancialStatementUrl = string.Empty;
                ModelState.Remove("CompanyFinancialDetail.AuditedFinancialStatementUrl");
            }

            if (!ModelState.IsValid)
            {
                LoadSelectLists();
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

        // Set by each handler: the user's company scope
        private CompanyScope _scope = default!;

        private void LoadSelectLists()
        {
            ViewData["CompanyId"] = new SelectList(_scope.Companies(_context.HousingCompanies).OrderBy(c => c.Name), "CompanyId", "Name");
        }

        private bool CompanyFinancialDetailExists(Guid id)
        {
            return _context.HousingCompanyFinancialDetails.Any(e => e.Id == id);
        }
    }
}
