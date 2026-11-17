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

namespace RadiantHomes.Pages.Projects
{
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<HouseProject> HouseProject { get;set; } = default!;

        // A Real Estate Admin without a company: show why the list is empty
        public bool NoCompany { get; set; }

        public async Task OnGetAsync()
        {
            // Visitors see the projects of approved companies; company staff their own company's; platform staff all.
            var scope = await CompanyScope.ForAsync(User, _context);
            NoCompany = scope.HasNoCompany;
            var query = scope.Projects(_context.Projects);
            if (!scope.IsMichuAdmin && !scope.IsRestricted)
            {
                query = query.Where(p => p.Company != null && p.Company.Status == RadiantHomes.Models.Security.CompanyStatuses.Approved);
            }
            HouseProject = await query.Include(h => h.Company).ToListAsync();
        }
    }
}
