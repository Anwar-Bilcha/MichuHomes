using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.RealEstates.OnBoarding.Profile
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<CreateModel> _logger;

        [BindProperty]
        public HousingCompany HousingCompany { get; set; } = default!;

        [BindProperty]
        public IFormFile? Logo { get; set; }

        public CreateModel(ApplicationDbContext context, IWebHostEnvironment env, ILogger<CreateModel> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Onboarding a new company is a Michu Admin task; a Real Estate Admin's "Company profile" is their own company
            var scope = await CompanyScope.ForAsync(User, _context);
            if (scope.IsRestricted)
            {
                return scope.CompanyId is Guid ownCompanyId
                    ? RedirectToPage("/RealEstates/Edit", new { id = ownCompanyId })
                    : Forbid();
            }

            HousingCompany = new HousingCompany();
            await PopulateAddressesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if ((await CompanyScope.ForAsync(User, _context)).IsRestricted) return Forbid();

            if (HousingCompany == null)
            {
                return BadRequest();
            }

            await CompanyForm.ValidateAsync(_context, ModelState, HousingCompany);
            CompanyForm.ValidateLogo(ModelState, Logo, nameof(Logo));

            if (ModelState.IsValid && await _context.HousingCompanies
                    .AnyAsync(h => h.Name == HousingCompany.Name && h.LicenseNumber == HousingCompany.LicenseNumber))
            {
                ModelState.AddModelError(string.Empty, "A housing company with the same name and license number already exists.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateAddressesAsync();
                return Page();
            }

            if (Logo != null && Logo.Length > 0)
            {
                // Web-accessible URL (not the physical path).
                HousingCompany.LogoUrl = await CompanyForm.SaveLogoAsync(_env, HousingCompany.Name, Logo);
            }

            // CreatedOn/CreatedBy are stamped by ApplicationDbContext.SaveChanges.
            HousingCompany.CompanyId = Guid.NewGuid();
            _context.HousingCompanies.Add(HousingCompany);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Housing company called {HousingCompany.Name} was successfully created!";
            return RedirectToPage("./FinancialDetails", new { companyId = HousingCompany.CompanyId });
        }

        private async Task PopulateAddressesAsync() =>
            ViewData["AddressId"] = await CompanyForm.AddressSelectListAsync(_context, HousingCompany?.AddressId);
    }
}
