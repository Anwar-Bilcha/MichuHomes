using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using System;
using System.Linq;
using System.Threading.Tasks;
using RadiantHomes.Services.AdminServices;
using Microsoft.AspNetCore.Identity; // Required for UserManager
using RadiantHomes.Models; // AppUser and Customer models are here

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.Projects
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager; // Inject UserManager

        public CreateModel(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        public SelectList Addresses { get; set; } = default!;

        public SelectList? AvailableHousingCompanies { get; set; }

        [BindProperty(SupportsGet = true)]
        public Guid CompanyId { get; set; }

        public HousingCompany? Company { get; set; }

        [BindProperty]
        public HouseProject HouseProject { get; set; } = new HouseProject();

        public bool ShowCompanyDropdown { get; set; } = false;

        public async Task<IActionResult> OnGetAsync(Guid? companyId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                // User is not logged in, redirect to login page
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            bool isSuperAdmin = await _userManager.IsInRoleAsync(currentUser, MichuHomesRoles.MichuAdmin);
            bool isRealEstateAdmin = await _userManager.IsInRoleAsync(currentUser, MichuHomesRoles.RealEstateAdmin);

            if (isSuperAdmin)
            {
                ShowCompanyDropdown = true;
                AvailableHousingCompanies = new SelectList(await _context.HousingCompanies.OrderBy(hc => hc.Name).ToListAsync(), "CompanyId", "Name");

                if (companyId.HasValue && companyId.Value != Guid.Empty)
                {
                    CompanyId = companyId.Value;
                    Company = await _context.HousingCompanies.FindAsync(CompanyId);
                    if (Company == null)
                    {
                        ModelState.AddModelError(string.Empty, "The specified Housing Company from the URL was not found. Please select an existing company from the dropdown.");
                        CompanyId = Guid.Empty; // Reset to allow re-selection
                    }
                }
            }
            else if (isRealEstateAdmin)
            {
                // Always their own company (AppUser.HousingCompanyId, else their profile's company); ?companyId= is ignored
                ShowCompanyDropdown = false;
                var scope = await CompanyScope.ForAsync(User, _context);
                if (scope.HasNoCompany)
                {
                    return RedirectToPage("./Index"); // the project list explains why there is nothing to do
                }
                if (scope.CompanyId is Guid ownCompanyId)
                {
                    CompanyId = ownCompanyId;
                    ModelState.Remove(nameof(CompanyId)); // so the hidden field shows their company, not the ?companyId= value
                    Company = await _context.HousingCompanies.FindAsync(CompanyId);
                    if (Company == null)
                    {
                        // Changed from NotFound() to ModelState.AddModelError
                        ModelState.AddModelError(string.Empty, $"Your associated housing company (ID: '{CompanyId}') could not be found. Please contact your administrator.");
                        CompanyId = Guid.Empty; // Invalidate CompanyId as it's not valid
                        // No return here, let the page render with the error.
                    }
                }
                else
                {
                    // Changed from Unauthorized() to ModelState.AddModelError
                    ModelState.AddModelError(string.Empty, "Your account is not linked to a specific housing company. Please contact your administrator.");
                    CompanyId = Guid.Empty; // Invalidate CompanyId
                    // No return here, let the page render with the error.
                }
            }
            else
            {
                // User is neither Dinar Admin nor Real Estate Admin. Changed from Forbid() to ModelState.AddModelError
                ModelState.AddModelError(string.Empty, "You do not have permission to create projects. Only 'Super Admin' or 'Real Estate Admin' roles can access this page.");
                return Page(); // Crucial: Render the page with the error and no project data
            }

            // Initialize HouseProject with the company determined above (Guid.Empty when none).
            // The start date defaults to today (an unset DateTime would show as 01/01/0001).
            HouseProject = new HouseProject
            {
                HousingCompanyId = CompanyId,
                StartDate = DateTime.Today,
            };

            await LoadAddressDropdownAsync(HouseProject.AddressId);

            // Optional: Add a model error if no addresses are available to select
            if (!Addresses.Any(item => item.Value != ""))
            {
                ModelState.AddModelError(string.Empty, "No addresses are available. Please add an address before creating a project.");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            bool isSuperAdmin = await _userManager.IsInRoleAsync(currentUser, MichuHomesRoles.MichuAdmin);
            bool isRealEstateAdmin = await _userManager.IsInRoleAsync(currentUser, MichuHomesRoles.RealEstateAdmin);

            ShowCompanyDropdown = isSuperAdmin; // Ensure UI state is correct on postback

            if (HouseProject == null)
            {
                ModelState.AddModelError(string.Empty, "Project data could not be submitted. Please try again.");
                await ReloadPageDependenciesAsync(isSuperAdmin); // Helper to reload data
                return Page();
            }

            if (!isSuperAdmin && isRealEstateAdmin)
            {
                var ownScope = await CompanyScope.ForAsync(User, _context);
                if (ownScope.CompanyId is Guid ownId) HouseProject.HousingCompanyId = ownId;
            }

            // For Dinar Admin, explicitly validate if a company was selected from the dropdown
            if (isSuperAdmin && HouseProject.HousingCompanyId == Guid.Empty)
            {
                ModelState.AddModelError("HouseProject.HousingCompanyId", "Please select a housing company for the project.");
            }

            // Check model validity (including validation attributes on HouseProject properties)
            if (!ModelState.IsValid)
            {
                await ReloadPageDependenciesAsync(isSuperAdmin, HouseProject.HousingCompanyId, HouseProject.AddressId);
                return Page();
            }

            // Securely set HousingCompanyId based on role: a Real Estate Admin's posted company id is ignored
            var scope = await CompanyScope.ForAsync(User, _context);
            if (scope.IsRestricted)
            {
                if (scope.CompanyId is not Guid ownCompanyId)
                {
                    return RedirectToPage("./Index"); // the project list explains why there is nothing to do
                }
                HouseProject.HousingCompanyId = ownCompanyId;
                CompanyId = ownCompanyId; // Keep CompanyId consistent for redirect
            }
            else if (isSuperAdmin)
            {
                // HouseProject.HousingCompanyId comes directly from the bound property (dropdown).
                // It was already checked above if empty for Dinar Admin.
                CompanyId = HouseProject.HousingCompanyId; // Ensure CompanyId matches selected for redirect
            }
            else
            {
                // User does not have appropriate roles. Changed from Forbid() to ModelState.AddModelError
                ModelState.AddModelError(string.Empty, "You do not have permission to create projects.");
                await ReloadPageDependenciesAsync(isSuperAdmin, HouseProject.HousingCompanyId, HouseProject.AddressId);
                return Page();
            }

            // Final check that we have a valid CompanyId before proceeding
            if (HouseProject.HousingCompanyId == Guid.Empty)
            {
                ModelState.AddModelError(string.Empty, "An error occurred: The associated housing company could not be determined for the project.");
                await ReloadPageDependenciesAsync(isSuperAdmin, HouseProject.HousingCompanyId, HouseProject.AddressId);
                return Page();
            }

            // CreatedOn/CreatedBy are stamped by ApplicationDbContext.SaveChanges
            HouseProject.ProjectId = Guid.NewGuid();

            _context.Projects.Add(HouseProject);

            try
            {
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Project created successfully!";
                return RedirectToPage("./Index", new { companyId = CompanyId });
            }
            catch (DbUpdateException ex)
            {
                ModelState.AddModelError(string.Empty, "Database error: An error occurred while saving the project. Please check your inputs. If the issue persists, contact support.");
                Console.WriteLine($"DbUpdateException: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                await ReloadPageDependenciesAsync(isSuperAdmin, HouseProject.HousingCompanyId, HouseProject.AddressId);
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again. If the issue persists, contact support.");
                Console.WriteLine($"Exception: {ex.Message}");
                await ReloadPageDependenciesAsync(isSuperAdmin, HouseProject.HousingCompanyId, HouseProject.AddressId);
                return Page();
            }
        }

        // Helper method to reload common page dependencies when returning to the page
        private async Task ReloadPageDependenciesAsync(bool isSuperAdmin, Guid? selectedCompanyId = null, Guid? selectedAddressId = null)
        {
            if (selectedCompanyId.HasValue && selectedCompanyId.Value != Guid.Empty)
            {
                Company = await _context.HousingCompanies.FindAsync(selectedCompanyId.Value);
            }
            else if (CompanyId != Guid.Empty)
            {
                // Fallback to CompanyId if selectedCompanyId is not provided
                Company = await _context.HousingCompanies.FindAsync(CompanyId);
            }
            else
            {
                // If neither, then Company remains null
                Company = null;
            }

            await LoadAddressDropdownAsync(selectedAddressId);

            if (isSuperAdmin)
            {
                AvailableHousingCompanies = new SelectList(await _context.HousingCompanies.OrderBy(hc => hc.Name).ToListAsync(), "CompanyId", "Name", selectedCompanyId);
            }
        }

        // Helper to load addresses dropdown
        private async Task LoadAddressDropdownAsync(Guid? selectedAddressId)
        {
            var addresses = await _context.Addresses
                .OrderBy(a => a.City)
                .Select(a => new
                {
                    a.AddressID,
                    FormattedAddressCompact = a.FormattedAddressCompact
                })
                .ToListAsync();

            Addresses = new SelectList(addresses, "AddressID", "FormattedAddressCompact", selectedAddressId);
        }
    }
}