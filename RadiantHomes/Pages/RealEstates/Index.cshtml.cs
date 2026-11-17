using System;
using System.Collections.Generic;
using RadiantHomes.Services.Security;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Pages.RealEstates
{
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<HousingCompany> HousingCompany { get;set; } = default!;

        public async Task OnGetAsync()
        {
            var access = await HttpContext.AccessAsync();
            var query = _context.HousingCompanies.Include(h => h.Address).AsQueryable();
            if (!access.Has(Perm.CompaniesView))
            {
                var own = access.CompanyId;
                query = query.Where(c => c.Status == RadiantHomes.Models.Security.CompanyStatuses.Approved || (own != null && c.CompanyId == own));
            }
            HousingCompany = await query.OrderBy(c => c.Name).ToListAsync();
        }
    }
}
