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
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<CompanyFinancialDetail> CompanyFinancialDetail { get;set; } = default!;

        // A Real Estate Admin without a company: show why the list is empty
        public bool NoCompany { get; set; }

        public async Task OnGetAsync()
        {
            // A Real Estate Admin only sees their own company's financial details
            var scope = await CompanyScope.ForAsync(User, _context);
            NoCompany = scope.HasNoCompany;
            CompanyFinancialDetail = await scope.Financials(_context.HousingCompanyFinancialDetails)
                .Include(c => c.Company).ToListAsync();
        }
    }
}
