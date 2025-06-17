using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization; // Needed for [Authorize] attribute
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore; // For async operations like FirstOrDefaultAsync, ToListAsync
using System.Linq; // For LINQ methods like Where, Select
using Microsoft.AspNetCore.Mvc.Rendering; // For SelectList
using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using RadiantHomes.Models; // For accessing user claims like NameIdentifier and Role

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.Financial
{
    [Authorize(Roles = MichuHomesRoles.MichuAdmin)]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public CreateModel(ApplicationDbContext context, UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [BindProperty]
        public CompanyFinancialDetail CompanyFinancialDetail { get; set; }

        // We'll manage CompanyId either from the user's profile or a UI selection
        public Guid CompanyId { get; set; }

        // This will hold the list of companies for the dropdown when needed
        public SelectList CompanySelectList { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? companyId)
        {
            // Determine the user's roles
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                // User is not logged in, redirect to login page
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }
            if (currentUser!=null)
            {
                var userRoles = await _userManager.GetRolesAsync(currentUser);
            }
            Console.WriteLine(currentUser?.HousingCompanyId);
            
            if(companyId.HasValue && companyId.Value !=Guid.Empty)
            {
                CompanyId = companyId.Value;
            }
            
            bool isRealEstateAdmin =await _userManager.IsInRoleAsync(currentUser, MichuHomesRoles.RealEstateAdmin);
            bool isSuperAdmin = await _userManager.IsInRoleAsync(currentUser, MichuHomesRoles.MichuAdmin);

            // 2) Logic for "Real estate admin": auto-fetch companyId
            if (isRealEstateAdmin && !isSuperAdmin) // If they are ONLY Real Estate Admin
            {
                var userId = currentUser.CustomerId;
                // Assuming HousingCompanyId is a property directly on your ApplicationUser model (AspNetUsers table)
                var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == userId);

                if (userProfile != null && userProfile.CompanyId != Guid.Empty)
                {
                    //CompanyId = userProfile.CompanyId.Value;
                    CompanyFinancialDetail = new CompanyFinancialDetail
                    {
                        CompanyId = CompanyId,
                        CreatedOn = DateTime.UtcNow
                    };
                    return Page();
                }
                else
                {
                    // Real Estate Admin must be associated with a company. If not, deny access.
                    // You might want a more specific error page or message here.
                    return Forbid("Your Real Estate Admin account is not associated with a housing company. Please contact support.");
                }
            }
            else if (isSuperAdmin)
            {
                if (companyId.HasValue && companyId.Value != Guid.Empty)
                {
                    // If companyId is passed in the URL (e.g., /Financial/Create?companyId=...), use it
                    CompanyId = companyId.Value;
                    CompanyFinancialDetail = new CompanyFinancialDetail
                    {
                        CompanyId =CompanyId,
                        CreatedOn = DateTime.UtcNow
                    };
                    return Page();
                }
                else
                {
                    // 3) If companyId is missing, populate SelectList for UI dropdown
                    await PopulateCompanySelectList();
                    CompanyFinancialDetail = new CompanyFinancialDetail
                    {
                        CreatedOn = DateTime.UtcNow, // Initialize for the form
                        CompanyId = CompanyId
                    };
                    return Page(); // Render the page with the dropdown
                }
            }
            else
            {
                // This scenario should be caught by the [Authorize] attribute,
                // but as a fallback, explicitly forbid access.
                return Forbid();
            }
        }

        // Helper method to populate the CompanySelectList
        private async Task PopulateCompanySelectList()
        {
            //var companies = await _context.HousingCompanies.OrderBy(c => c.Name).ToListAsync();
            CompanySelectList = new SelectList(await _context.HousingCompanies.OrderBy(hc => hc.Name).ToListAsync(), "CompanyId", "Name");

            //CompanySelectList = new SelectList(companies, "CompanyId", "Name");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Re-evaluate user roles on POST for security and correct handling
            //var userRoles = ((ClaimsIdentity)User.Identity).Claims
            //    .Where(c => c.Type == ClaimTypes.Role)
            //    .Select(c => c.Value)
            //    .ToList();
            var user = await _userManager.GetUserAsync(User);
            if(user==null)
            {
                RedirectToPage("/Identity/Account/Login");
            }
            bool isRealEstateAdmin = await _userManager.IsInRoleAsync(user,MichuHomesRoles.RealEstateAdmin);
            bool isSuperAdmin = await _userManager.IsInRoleAsync(user, MichuHomesRoles.MichuAdmin);

            // Validate the model state (e.g., required fields in CompanyFinancialDetail)
            if (!ModelState.IsValid)
            {
                // re-populate the dropdown so it's not empty on page reload.
                if (isSuperAdmin && CompanyId == Guid.Empty)
                {
                    await PopulateCompanySelectList();
                }
                return Page();
            }

            // Assign CompanyId based on the user's role
            if (isRealEstateAdmin && !isSuperAdmin)
            {
                // For Real Estate Admin, always use their assigned companyId for submission
                var userId = user?.CustomerId;
                var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == userId);

                if (userProfile != null && userProfile.CompanyId != Guid.Empty && userProfile.CompanyId != null)
                {
                    CompanyFinancialDetail.CompanyId = userProfile.CompanyId.Value;
                }
                else
                {
                    // This indicates a configuration issue, prevent saving
                    ModelState.AddModelError(string.Empty, "Your Real Estate Admin account is not associated with a housing company.");
                    return Page(); 
                    // Stay on the page with an error message
                }
            }
            else if (isSuperAdmin)
            {
                // For Dinar Admin, use the CompanyId bound from the UI (either hidden or selected from dropdown)
                if (CompanyId != Guid.Empty)
                {
                    CompanyFinancialDetail.CompanyId = CompanyId;
                }
                else
                {
                    // If Dinar Admin and no CompanyId was selected/provided, show an error
                    ModelState.AddModelError("CompanyId", "Please select a company.");
                    await PopulateCompanySelectList(); // Re-populate for display
                    return Page(); // Stay on the page with an error
                }
            }
            else
            {
                // Fallback for unauthorized access attempts during POST
                return Forbid();
            }

            CompanyFinancialDetail.CreatedOn = DateTime.UtcNow;
            CompanyFinancialDetail.CreatedBy = User.Identity?.Name ?? "System";

            _context.HousingCompanyFinancialDetails.Add(CompanyFinancialDetail);
            try
            {
                await _context.SaveChangesAsync();

            }
catch(Exception e)
            {
                ModelState.AddModelError("Update Db Error", $"{e.InnerException?.Message??e.Message}");

            }
            // Redirect to the next logical step, e.g., creating a project for this company
            return RedirectToPage("/HousingCompanyAdmin/Projects/Create", new { companyId = CompanyFinancialDetail.CompanyId });
        }
    }
}