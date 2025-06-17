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
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<CompanyFinancialDetail> CompanyFinancialDetail { get;set; } = default!;

        public async Task OnGetAsync()
        {
            CompanyFinancialDetail = await _context.HousingCompanyFinancialDetails
                .Include(c => c.Company).ToListAsync();
        }
    }
}
