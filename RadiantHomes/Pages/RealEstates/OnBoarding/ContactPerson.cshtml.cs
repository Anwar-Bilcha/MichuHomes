using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.RealEstates.OnBoarding
{
    /// <summary>
    /// Onboarding step 3. The contact person (the company's real estate admin) is created by the existing
    /// HouseOwner/Create flow in its Michu-admin "pre-creation" mode, which needs a proposed profile id and the
    /// company id. This step explains that, links to it and lists the contact persons already linked to the company.
    /// </summary>
    public class ContactPersonModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ContactPersonModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public Guid? CompanyId { get; set; }

        public HousingCompany? Company { get; set; }
        public List<Customer> ContactPersons { get; set; } = new();
        public SelectList? CompanySelectList { get; set; }

        /// <summary>Id the new contact person's profile will get (HouseOwner/Create's "UserId" parameter).</summary>
        public Guid ProposedContactPersonId { get; } = Guid.NewGuid();

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

            ContactPersons = await _context.UserProfiles.AsNoTracking()
                .Where(p => p.CompanyId == Company.CompanyId)
                .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
                .ToListAsync();

            return Page();
        }
    }
}
