using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.RealEstates.OnBoarding
{
    /// <summary>
    /// Onboarding step 4. Housing projects are created by the existing Projects/Create page, which takes the
    /// company as ?companyId=…; this step links to it (with a return URL back here) and lists the company's projects.
    /// </summary>
    public class HouseProjectsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public HouseProjectsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public Guid? CompanyId { get; set; }

        public HousingCompany? Company { get; set; }
        public List<HouseProject> Projects { get; set; } = new();
        public SelectList? CompanySelectList { get; set; }

        // A Real Estate Admin without a company: show why there is nothing here
        public bool NoCompany { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Real Estate Admins only onboard their own company
            var scope = await CompanyScope.ForAsync(User, _context);
            if (scope.IsRestricted)
            {
                if (scope.CompanyId is not Guid ownCompanyId)
                {
                    NoCompany = true;
                    return Page();
                }
                if (CompanyId is Guid requested && requested != Guid.Empty && requested != ownCompanyId)
                {
                    return Forbid();
                }
                CompanyId = ownCompanyId;
            }

            if (CompanyId is Guid companyId && companyId != Guid.Empty)
            {
                Company = await _context.HousingCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
            }

            if (Company == null)
            {
                // Opened without (or with an unknown) company: let the admin pick the company being onboarded.
                CompanyId = null;
                var companies = await _context.HousingCompanies.AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new { c.CompanyId, c.Name })
                    .ToListAsync();
                CompanySelectList = new SelectList(companies, "CompanyId", "Name");
                return Page();
            }

            Projects = await _context.Projects.AsNoTracking()
                .Where(p => p.HousingCompanyId == Company.CompanyId)
                .OrderByDescending(p => p.CreatedOn)
                .ToListAsync();

            return Page();
        }
    }
}
