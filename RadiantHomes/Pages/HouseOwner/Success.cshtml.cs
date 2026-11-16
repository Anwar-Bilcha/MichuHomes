using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.HouseOwner
{
    // Shown after a house owner profile + account have been created (HouseOwner/Create → Register → here).
    public class SuccessModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public SuccessModel(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public int DaysWait { get; set; } = 2;
        public string companyEmail { get; set; } = "info@michuhomes.et";
        public string? ApplicantName { get; set; }

        public async Task OnGetAsync()
        {
            // Set by HouseOwner/Create from the profile it just created.
            ApplicantName = TempData["ApplicantName"] as string;

            // Fallback: the signed-in user's linked profile (e.g. TempData already consumed).
            if (string.IsNullOrWhiteSpace(ApplicantName) && User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user?.CustomerId is Guid customerId)
                {
                    var profile = await _context.UserProfiles.AsNoTracking()
                        .Where(c => c.Id == customerId)
                        .Select(c => new { c.FirstName, c.LastName })
                        .FirstOrDefaultAsync();
                    if (profile != null)
                    {
                        ApplicantName = $"{profile.FirstName} {profile.LastName}".Trim();
                    }
                }
            }
        }
    }
}
