using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.RealEstates.OnBoarding.FinancialDetails
{

    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public CreateModel(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public CompanyFinancialDetail CompanyFinancialDetail { get; set; } = new();
        [BindProperty]
        public Guid SelectedCompanyId { get; set; }
        public SelectList? CompanySelectList { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        private bool IsRealEstateAdmin { get; set; }
        public bool IsSuperAdmin { get; private set; } // platform staff (pick any company)

        public async Task<IActionResult> OnGetAsync(Guid? companyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            IsRealEstateAdmin = (await RadiantHomes.Services.AdminServices.CompanyScope.ForAsync(User, _context)).IsRestricted;
            IsSuperAdmin = (await RadiantHomes.Services.AdminServices.CompanyScope.ForAsync(User, _context)).IsMichuAdmin;

            if (!IsRealEstateAdmin && !IsSuperAdmin)
                return Forbid();
            CompanyFinancialDetail = new CompanyFinancialDetail();

            if (IsSuperAdmin)
            {
                // Coming from step 1 the company is known; otherwise the Michu admin picks one.
                if (companyId.HasValue && companyId.Value != Guid.Empty)
                {
                    var company = await _context.HousingCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId.Value);
                    if (company != null)
                    {
                        SelectedCompanyId = company.CompanyId;
                        CompanyFinancialDetail.CompanyId = company.CompanyId;
                        CompanyName = company.Name;
                    }
                }
                if (SelectedCompanyId == Guid.Empty)
                    await PopulateCompanySelectList();
            }
            else // Real estate (housing company) admin: always their own company
            {
                var ownCompanyId = (await CompanyScope.ForAsync(User, _context)).CompanyId;
                if (companyId.HasValue && companyId.Value != Guid.Empty && companyId != ownCompanyId)
                    return Forbid();
                var company = ownCompanyId == null
                    ? null
                    : await _context.HousingCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == ownCompanyId);
                if (company == null)
                {
                    TempData["ErrorMessage"] = "Your account is not associated with a housing company.";
                    return RedirectToPage("/RealEstates/Index");
                }
                SelectedCompanyId = company.CompanyId;
                CompanyFinancialDetail.CompanyId = company.CompanyId; // Ensure this is set for model binding
                CompanyName = company.Name;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            IsRealEstateAdmin = (await RadiantHomes.Services.AdminServices.CompanyScope.ForAsync(User, _context)).IsRestricted;
            IsSuperAdmin = (await RadiantHomes.Services.AdminServices.CompanyScope.ForAsync(User, _context)).IsMichuAdmin;

            if (!IsRealEstateAdmin && !IsSuperAdmin)
                return Forbid();

            // The statement URL input is disabled (so not posted) unless an audited statement is ticked;
            // the column is NOT NULL, so store an empty string instead of failing validation.
            if (!CompanyFinancialDetail.hasAuditedFinancialStatement)
            {
                ModelState.Remove($"{nameof(CompanyFinancialDetail)}.{nameof(CompanyFinancialDetail.AuditedFinancialStatementUrl)}");
                CompanyFinancialDetail.AuditedFinancialStatementUrl = string.Empty;
            }

            if (IsRealEstateAdmin && !IsSuperAdmin)
            {
                // AppUser.HousingCompanyId, else the profile's company (CompanyScope); the posted company id is ignored
                var ownCompanyId = (await CompanyScope.ForAsync(User, _context)).CompanyId;

                if (ownCompanyId.HasValue && ownCompanyId.Value != Guid.Empty)
                {
                    CompanyFinancialDetail.CompanyId = ownCompanyId.Value;
                    SelectedCompanyId = ownCompanyId.Value;
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Your Real Estate Admin account is not associated with a housing company.");
                }
            }
            else if (IsSuperAdmin)
            {
                if (SelectedCompanyId != Guid.Empty && await _context.HousingCompanies.AnyAsync(c => c.CompanyId == SelectedCompanyId))
                {
                    CompanyFinancialDetail.CompanyId = SelectedCompanyId;
                }
                else
                {
                    SelectedCompanyId = Guid.Empty;
                    ModelState.AddModelError(nameof(SelectedCompanyId), "Please select a company.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadCompanyDisplayAsync();
                return Page();
            }

            CompanyFinancialDetail.Id = Guid.NewGuid(); // The model expects a NEWID() column default that the database no longer has, so the key is generated here.
            CompanyFinancialDetail.CreatedOn = DateTime.UtcNow;
            CompanyFinancialDetail.CreatedBy = User.Identity?.Name ?? "System";

            _context.HousingCompanyFinancialDetails.Add(CompanyFinancialDetail);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"Database error: {ex.InnerException?.Message ?? ex.Message}");
                ModelState.AddModelError(string.Empty, "An error occurred while saving the financial details.");
                await LoadCompanyDisplayAsync();
                return Page();
            }

            TempData["SuccessMessage"] = "Financial details saved.";
            // Step 3 of the onboarding flow: the company's contact person.
            return RedirectToPage("./ContactPerson", new { companyId = CompanyFinancialDetail.CompanyId });
        }

        /// <summary>Re-populates what the page shows for the company after a failed POST.</summary>
        private async Task LoadCompanyDisplayAsync()
        {
            if (SelectedCompanyId == Guid.Empty)
            {
                await PopulateCompanySelectList();
                return;
            }
            CompanyName = await _context.HousingCompanies
                .Where(c => c.CompanyId == SelectedCompanyId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync() ?? string.Empty;
        }

        private async Task PopulateCompanySelectList()
        {
            var companies = await _context.HousingCompanies
                                          .OrderBy(h => h.Name)
                                          .Select(h => new { h.CompanyId, h.Name })
                                          .ToListAsync();
            CompanySelectList = new SelectList(companies, "CompanyId", "Name", SelectedCompanyId == Guid.Empty ? null : SelectedCompanyId);
        }
    }
}
