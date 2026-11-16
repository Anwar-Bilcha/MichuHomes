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
    public class DetailsModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public CompanyFinancialDetail CompanyFinancialDetail { get; set; } = default!;

        // id = the financial-detail record's Id (same route value as Edit and Delete).
        // companyId is the old name of the same value, still accepted so existing links keep working.
        public async Task<IActionResult> OnGetAsync(Guid? id, Guid? companyId)
        {
            id ??= companyId;
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
    }
}
