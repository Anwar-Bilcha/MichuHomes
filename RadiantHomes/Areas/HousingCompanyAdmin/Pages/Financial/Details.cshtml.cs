using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

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

        public async Task<IActionResult> OnGetAsync(Guid? companyId)
        {
            if (companyId == null)
            {
                return NotFound();
            }

            var companyfinancialdetail = await _context.HousingCompanyFinancialDetails.FirstOrDefaultAsync(m => m.Id == companyId);

            if (companyfinancialdetail is not null)
            {
                CompanyFinancialDetail = companyfinancialdetail;

                return Page();
            }

            return NotFound();
        }
    }
}
