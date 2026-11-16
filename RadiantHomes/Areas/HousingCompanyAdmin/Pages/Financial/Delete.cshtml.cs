using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.Financial
{
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context)
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

            var companyfinancialdetail = await _context.HousingCompanyFinancialDetails
                .Include(c => c.Company)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (companyfinancialdetail is not null)
            {
                // Real Estate Admins may only open their own company's records
                if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(companyfinancialdetail.CompanyId))
                {
                    return Forbid();
                }
                CompanyFinancialDetail = companyfinancialdetail;

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

            var companyfinancialdetail = await _context.HousingCompanyFinancialDetails.FindAsync(id);
            if (companyfinancialdetail != null)
            {
                if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(companyfinancialdetail.CompanyId))
                {
                    return Forbid();
                }
                CompanyFinancialDetail = companyfinancialdetail;
                _context.HousingCompanyFinancialDetails.Remove(CompanyFinancialDetail);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
