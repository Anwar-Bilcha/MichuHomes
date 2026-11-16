#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Areas.Identity.Pages.Account
{
    // AdminRegister reuses the Register handlers (company / customer linking lives there) but is
    // only for admins: it lists companies and approved customer profiles, and registering with a
    // company grants the Real Estate Admin role.
    [Authorize(Roles = MichuHomesRoles.MichuAdmin + "," + MichuHomesRoles.RealEstateAdmin)]
    public class AdminRegisterModel : RegisterModel
    {
        public AdminRegisterModel(
            UserManager<AppUser> userManager,
            IUserStore<AppUser> userStore,
            SignInManager<AppUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context)
            : base(userManager, userStore, signInManager, logger, emailSender, context)
        {
        }
    }
}
