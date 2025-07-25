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
        public CompanyFinancialDetail CompanyFinancialDetail { get; set; }
        [BindProperty]
        public Guid SelectedCompanyId { get; set; }
        public SelectList? CompanySelectList { get; set; }
        public string CompanyName { get; set; }
        private bool IsRealEstateAdmin { get; set; }
        private bool IsSuperAdmin { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? companyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            IsRealEstateAdmin = await _userManager.IsInRoleAsync(currentUser, MichuHomesRoles.RealEstateAdmin);
            IsSuperAdmin = await _userManager.IsInRoleAsync(currentUser, MichuHomesRoles.MichuAdmin);

            if (!IsRealEstateAdmin && !IsSuperAdmin)
                return Forbid();
            CompanyFinancialDetail = new CompanyFinancialDetail();

            if (User.IsInRole(MichuHomesRoles.MichuAdmin))
            {
                CompanySelectList = new SelectList(_context.HousingCompanies.OrderBy(c => c.Name), "Id", "Name");
                if (companyId.HasValue && companyId.Value != Guid.Empty)
                {
                    SelectedCompanyId = companyId.Value;
                    CompanyFinancialDetail.CompanyId = companyId.Value;
                    // You might want to fetch the company name here too if coming from a link
                    CompanyName = _context.HousingCompanies.FirstOrDefault(c => c.CompanyId == companyId.Value)?.Name??"";
                }
            }
            else // HousingCompanyAdmin
            {
                var user = await _userManager.GetUserAsync(User);
                var company = _context.HousingCompanies.FirstOrDefault(c => c.CompanyId == user.HousingCompanyId);
                if (company == null)
                {
                    TempData["error"] = "Associated company not found.";
                    return RedirectToPage("./Index"); // Or some other error handling
                }
                SelectedCompanyId = company.CompanyId;
                CompanyFinancialDetail.CompanyId = company.CompanyId; // Ensure this is set for model binding
                CompanyName = company.Name; // Populate the new property
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            IsRealEstateAdmin = await _userManager.IsInRoleAsync(currentUser, MichuHomesRoles.RealEstateAdmin);
            IsSuperAdmin = await _userManager.IsInRoleAsync(currentUser, MichuHomesRoles.MichuAdmin);

            if (!IsRealEstateAdmin && !IsSuperAdmin)
                return Forbid();

            if (!ModelState.IsValid)
            {
                if (IsSuperAdmin && SelectedCompanyId == Guid.Empty)
                    await PopulateCompanySelectList();

                return Page();
            }

            if (IsRealEstateAdmin && !IsSuperAdmin)
            {
                var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == currentUser.CustomerId);

                if (userProfile?.CompanyId != null && userProfile.CompanyId != Guid.Empty)
                {
                    CompanyFinancialDetail.CompanyId = userProfile.CompanyId.Value;
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Your Real Estate Admin account is not associated with a housing company.");
                    return Page();
                }
            }
            else if (IsSuperAdmin)
            {
                if (SelectedCompanyId != Guid.Empty)
                {
                    CompanyFinancialDetail.CompanyId = SelectedCompanyId;
                }
                else
                {
                    ModelState.AddModelError("SelectedCompanyId", "Please select a company.");
                    await PopulateCompanySelectList();
                    return Page();
                }
            }

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
                if (IsSuperAdmin && SelectedCompanyId == Guid.Empty)
                    await PopulateCompanySelectList();

                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please contact support.");
                if (IsSuperAdmin && SelectedCompanyId == Guid.Empty)
                    await PopulateCompanySelectList();

                return Page();
            }

            return RedirectToPage("/Projects/Create", new { companyId = CompanyFinancialDetail.CompanyId });
        }

        private async Task PopulateCompanySelectList()
        {
            var companies = await _context.HousingCompanies
                                          .OrderBy(h => h.Name)
                                          .Select(h => new { h.CompanyId, h.Name })
                                          .ToListAsync();
            CompanySelectList = new SelectList(companies, "CompanyId", "Name");
        }
    }
}
