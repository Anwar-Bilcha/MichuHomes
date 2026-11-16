using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.Projects
{
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>The company the project belongs to (?companyId=…).</summary>
        [BindProperty(SupportsGet = true)] public Guid CompanyId { get; set; }

        /// <summary>Optional local URL to go back to after saving (e.g. the onboarding step).</summary>
        [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

        public HousingCompany Company { get; set; } = new HousingCompany();
        public SelectList? AddressSelectList { get; set; }

        /// <summary>True when the page was opened without ?companyId (e.g. "New project" in the menu): the form then has a company dropdown.</summary>
        [BindProperty] public bool PickCompany { get; set; }
        public SelectList? CompanySelectList { get; set; }

        /// <summary>Where the page goes after the success message: the return URL when it is local, else the project list.</summary>
        public string DoneUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Page("./Index")!;

        public async Task<IActionResult> OnGetAsync()
        {
            // A Real Estate Admin always creates projects for their own company (a ?companyId= for another company is ignored)
            var scope = await CompanyScope.ForAsync(User, _context);
            if (scope.HasNoCompany) return RedirectToPage("./Index"); // the list explains why
            if (scope.IsRestricted)
            {
                CompanyId = scope.CompanyId!.Value;
                ModelState.Remove(nameof(CompanyId)); // so the hidden field shows their company, not the ?companyId= value
            }

            // Dates default to today (an unset DateTime would show as 01/01/0001); the end date stays empty (ongoing).
            HouseProject = new HouseProject { StartDate = DateTime.Today, EndDate = null };
            PickCompany = CompanyId == default;

            if (!PickCompany)
            {
                var company = await _context.HousingCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == CompanyId);
                if (company == null)
                {
                    return NotFound();
                }
                Company = company;
                HouseProject.HousingCompanyId = CompanyId;
                HouseProject.AddressId = company.AddressId;
            }

            await PopulateListsAsync();
            return Page();
        }

        [BindProperty]
        public HouseProject HouseProject { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // A Real Estate Admin's project always belongs to their own company, whatever company id is posted.
            var scope = await CompanyScope.ForAsync(User, _context);
            if (scope.HasNoCompany) return RedirectToPage("./Index");
            if (scope.IsRestricted)
            {
                CompanyId = scope.CompanyId!.Value;
                PickCompany = false;
            }

            // CompanyId comes from the hidden field (opened with ?companyId) or from the company dropdown.
            var company = CompanyId == default
                ? null
                : await _context.HousingCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == CompanyId);
            ModelState.Remove(nameof(CompanyId));
            if (company == null)
            {
                ModelState.AddModelError(nameof(CompanyId), "Please select the company this project belongs to.");
            }
            else
            {
                Company = company;
            }

            // Navigations are never posted; the foreign keys are checked instead.
            ModelState.Remove($"{nameof(HouseProject)}.{nameof(HouseProject.Company)}");
            ModelState.Remove($"{nameof(HouseProject)}.{nameof(HouseProject.Address)}");
            ModelState.Remove($"{nameof(HouseProject)}.{nameof(HouseProject.HousingCompanyId)}");
            // An empty address choice is a binding error ("The value '' is invalid."); replaced by the check below.
            ModelState.Remove($"{nameof(HouseProject)}.{nameof(HouseProject.AddressId)}");

            if (HouseProject != null && !await _context.Addresses.AnyAsync(a => a.AddressID == HouseProject.AddressId))
            {
                ModelState.AddModelError($"{nameof(HouseProject)}.{nameof(HouseProject.AddressId)}", "Please select the project's address.");
            }
            if (HouseProject?.EndDate is DateTime end && end < HouseProject.StartDate)
            {
                ModelState.AddModelError($"{nameof(HouseProject)}.{nameof(HouseProject.EndDate)}", "End Date cannot be before the Start Date.");
            }

            if (HouseProject == null || !ModelState.IsValid)
            {
                HouseProject ??= new HouseProject { StartDate = DateTime.Today };
                await PopulateListsAsync();
                return Page();
            }

            // CreatedOn/CreatedBy are stamped by ApplicationDbContext.SaveChanges.
            HouseProject.HousingCompanyId = CompanyId;
            HouseProject.ProjectId = Guid.NewGuid();
            _context.Projects.Add(HouseProject);
            await _context.SaveChangesAsync();

            // Standard post-redirect-get: the toast is shown on the page the user returns to.
            TempData["Success"] = $"Project “{HouseProject.ProjectName}” created.";
            return LocalRedirect(DoneUrl);
        }

        private async Task PopulateListsAsync()
        {
            if (PickCompany)
            {
                var companies = await _context.HousingCompanies.AsNoTracking().OrderBy(c => c.Name).Select(c => new { c.CompanyId, c.Name }).ToListAsync();
                CompanySelectList = new SelectList(companies, "CompanyId", "Name", CompanyId == default ? null : CompanyId);
            }

            var addresses = await _context.Addresses.AsNoTracking().ToListAsync();
            AddressSelectList = new SelectList(
                addresses.OrderBy(a => a.FormattedAddressCompact).Select(a => new { a.AddressID, Text = a.FormattedAddressCompact }),
                "AddressID", "Text", HouseProject?.AddressId);
        }
    }
}
