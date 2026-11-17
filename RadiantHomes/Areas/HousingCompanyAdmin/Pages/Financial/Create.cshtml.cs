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

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.Financial
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
        public CompanyFinancialDetail CompanyFinancialDetail { get; set; } = default!;
        // Set when the company is fixed (company admin, or a Michu Admin arriving with ?companyId=)
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
                if (companyId.HasValue && companyId.Value != Guid.Empty
                    && await _context.HousingCompanies.AnyAsync(c => c.CompanyId == companyId.Value))
                {
                    SelectedCompanyId = companyId.Value;
                    CompanyFinancialDetail.CompanyId = companyId.Value;
                }
            }
            else // Real Estate (company) admin: always their own company
            {
                var ownCompanyId = await GetOwnCompanyIdAsync(currentUser);
                if (ownCompanyId == null)
                {
                    TempData["error"] = "Associated company not found.";
                    return RedirectToPage("./Index");
                }
                SelectedCompanyId = ownCompanyId.Value;
                CompanyFinancialDetail.CompanyId = ownCompanyId.Value;
            }

            await LoadFormDataAsync();
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

            // Which company the record belongs to
            if (IsSuperAdmin)
            {
                // Either fixed by ?companyId= (SelectedCompanyId) or chosen in the company dropdown
                var chosen = SelectedCompanyId != Guid.Empty ? SelectedCompanyId : CompanyFinancialDetail.CompanyId;
                if (chosen == Guid.Empty || !await _context.HousingCompanies.AnyAsync(c => c.CompanyId == chosen))
                {
                    ModelState.Remove("CompanyFinancialDetail.CompanyId"); // replaces the binder's "The value '' is invalid."
                    ModelState.AddModelError("CompanyFinancialDetail.CompanyId", "Please select a company.");
                }
                CompanyFinancialDetail.CompanyId = chosen;
            }
            else
            {
                var ownCompanyId = await GetOwnCompanyIdAsync(currentUser);
                if (ownCompanyId == null)
                {
                    ModelState.AddModelError(string.Empty, "Your Real Estate Admin account is not associated with a housing company.");
                }
                else
                {
                    SelectedCompanyId = ownCompanyId.Value;
                    CompanyFinancialDetail.CompanyId = ownCompanyId.Value;
                }
            }

            // The statement URL is only asked for when an audited statement exists (the field is disabled otherwise)
            if (!CompanyFinancialDetail.hasAuditedFinancialStatement)
            {
                CompanyFinancialDetail.AuditedFinancialStatementUrl = string.Empty;
                ModelState.Remove("CompanyFinancialDetail.AuditedFinancialStatementUrl");
            }

            if (!ModelState.IsValid)
            {
                await LoadFormDataAsync();
                return Page();
            }

            // The EF model declares NEWID() as the key default, but the database column has no default,
            // so EF would insert NULL for an empty Guid key: generate it here (as Projects/Create does).
            // CreatedOn/CreatedBy are stamped by ApplicationDbContext.SaveChanges
            CompanyFinancialDetail.Id = Guid.NewGuid();

            _context.HousingCompanyFinancialDetails.Add(CompanyFinancialDetail);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"Database error: {ex.InnerException?.Message ?? ex.Message}");
                ModelState.AddModelError(string.Empty, "An error occurred while saving the financial details.");
                await LoadFormDataAsync();
                return Page();
            }

            return RedirectToPage("/Projects/Create", new { companyId = CompanyFinancialDetail.CompanyId });
        }

        // The company admin's own company: AppUser.HousingCompanyId, falling back to their Customer profile's CompanyId
        private async Task<Guid?> GetOwnCompanyIdAsync(AppUser user)
        {
            Guid? companyId = user.HousingCompanyId; // from the account only
            if (companyId == null || companyId == Guid.Empty)
                return null;

            return await _context.HousingCompanies.AnyAsync(c => c.CompanyId == companyId) ? companyId : null;
        }

        // Company name for a fixed company, or the company dropdown for a Michu Admin
        private async Task LoadFormDataAsync()
        {
            if (SelectedCompanyId != Guid.Empty)
            {
                CompanyName = await _context.HousingCompanies
                    .Where(c => c.CompanyId == SelectedCompanyId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync() ?? string.Empty;
            }
            else
            {
                var companies = await _context.HousingCompanies
                    .OrderBy(h => h.Name)
                    .Select(h => new { h.CompanyId, h.Name })
                    .ToListAsync();
                CompanySelectList = new SelectList(companies, "CompanyId", "Name");
            }
        }
    }
}
