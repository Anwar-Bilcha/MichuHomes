// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using RadiantHomes.Services.Security;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Services.CustomerPortal;

namespace RadiantHomes.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly IUserStore<AppUser> _userStore;
        private readonly IUserEmailStore<AppUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;

        public RegisterModel(
            UserManager<AppUser> userManager,
            IUserStore<AppUser> userStore,
            SignInManager<AppUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
        }

        public string CustomerProfilePicture { get; set; }
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel 
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            public bool IsEmployee { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
        }
        [BindProperty(SupportsGet = true)]
        public Guid? CustomerId { get; set; }
        [BindProperty(SupportsGet = true)]
        public Guid? CompanyId { get; set; }
        public string Success { get; set; }

        /// <summary>Referral code of a friend's invitation link (/r/{code}); the link also keeps it in a cookie.</summary>
        [BindProperty(SupportsGet = true)]
        public string? Ref { get; set; }

        /// <summary>First name of the customer whose invitation brought the visitor here.</summary>
        public string? InvitedBy { get; private set; }

        private async Task ResolveReferralAsync(ReferralService referrals)
        {
            var referral = await referrals.FindActiveAsync(Ref ?? Request.Cookies[ReferralService.CookieName]);
            Ref = referral?.Code;
            InvitedBy = referral?.Customer?.FirstName;
        }

        // customerId / companyId are optional query parameters (bound into CustomerId / CompanyId above):
        //   - /Identity/Account/Register                         plain sign-up (Login page link)
        //   - ?customerId=<profile id>                            link the new account to a just-created
        //                                                         house owner profile (HouseOwner/Create)
        //   - ?companyId=<company id>                             register a Real Estate Admin of that company
        //                                                         (AdminRegister, admin pre-creation flow)
        // A value that is missing, malformed or not usable is ignored so the visitor can still register.
        public async Task<IActionResult> OnGetAsync([FromServices] ReferralService referrals, string returnUrl = null)
        {
            // Invalid GUIDs leave binding errors (and their raw values) in ModelState; drop them so the
            // hidden fields render from the cleaned-up properties below.
            ModelState.Clear();
            await ResolveReferralAsync(referrals);

            if (CustomerId.HasValue && await GetCustomerLinkErrorAsync(CustomerId.Value) != null)
            {
                CustomerId = null;
            }
            if (CompanyId.HasValue && !await CanAssignCompanyAsync(CompanyId.Value))
            {
                CompanyId = null;
            }

            ReturnUrl = returnUrl ?? Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            await LoadSelectListsAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync([FromServices] ReferralService referrals, string returnUrl = null)
        {
            await ResolveReferralAsync(referrals);
            returnUrl ??= Url.Content("~/");
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (CustomerId.HasValue)
            {
                var linkError = await GetCustomerLinkErrorAsync(CustomerId.Value);
                if (linkError != null)
                {
                    ModelState.AddModelError(string.Empty, linkError);
                }
            }
            if (CompanyId.HasValue && !await CanAssignCompanyAsync(CompanyId.Value))
            {
                ModelState.AddModelError(string.Empty, "You are not allowed to register an admin for the selected company.");
            }

            if (ModelState.IsValid)
            {

               var user = CreateUser();
                //var user = new AppUser { CustomerId=CustomerId };
                // user.CustomerId = CustomerId;
                if (CustomerId.HasValue)
                {
                    user.CustomerId = CustomerId.Value;
                }
                if (CompanyId.HasValue)
                {
                    // A company's administrator (onboarding): company staff, named after their pre-created profile.
                    user.HousingCompanyId = CompanyId.Value;
                    user.AccountType = RadiantHomes.Models.Security.AccountType.CompanyStaff;
                    user.FullName = CustomerId.HasValue
                        ? await _context.UserProfiles.Where(p => p.Id == CustomerId.Value).Select(p => (p.FirstName + " " + p.LastName).Trim()).FirstOrDefaultAsync()
                        : null;
                }
                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");
                    if (CompanyId.HasValue)
                    {
                        await AssignCompanySuperAdminAsync(user, CompanyId.Value);
                    }
                    else
                    {
                        await _userManager.AddToRoleAsync(user, MichuHomesRoles.Buyer);
                        await _userManager.AddToRoleAsync(user, MichuHomesRoles.IndividualHouseOwner);
                    }
                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page("/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);
                    if (string.IsNullOrEmpty(callbackUrl))
                    {
                        throw new InvalidOperationException("callbackUrl is null or empty.");
                    }
                    _logger.LogInformation("UserId: {UserId}", userId);
                    _logger.LogInformation("ReturnUrl: {ReturnUrl}", returnUrl);
                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    // A friend's invitation (customer portal, "Refer a friend"): never blocks registration.
                    if (User.Identity?.IsAuthenticated != true && !string.IsNullOrEmpty(Ref))
                    {
                        try
                        {
                            await referrals.RecordAsync(Ref, user);
                            Response.Cookies.Delete(ReferralService.CookieName);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not record the referral {Code} for the new account.", Ref);
                        }
                    }

                    // Someone who is already signed in (e.g. an admin on AdminRegister) is registering an
                    // account for another person: keep them signed in as themselves.
                    if (User.Identity?.IsAuthenticated == true)
                    {
                        Success = $"The account {Input.Email} has been created.";
                        ModelState.Clear();
                        Input = new InputModel();
                        CustomerId = null;
                        CompanyId = null;
                        await LoadSelectListsAsync();
                        return Page();
                    }

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            await LoadSelectListsAsync();
            return Page();
        }

        // Returns why the profile can't be linked to a new account, or null when it can.
        private async Task<string> GetCustomerLinkErrorAsync(Guid customerId)
        {
            if (!await _context.UserProfiles.AnyAsync(c => c.Id == customerId))
            {
                return "The customer profile to link to this account was not found.";
            }
            // AspNetUsers.CustomerId is unique: one account per profile.
            if (await _userManager.Users.AnyAsync(u => u.CustomerId == customerId))
            {
                return "This customer profile is already linked to an account.";
            }
            return null;
        }

        // Registering with a company creates that company's administrator (Company Super Admin): part of onboarding,
        // so only platform staff who onboard companies or manage users may do it. Company staff add colleagues on
        // Staff/Users, where they can only give roles within their own permissions.
        private async Task<bool> CanAssignCompanyAsync(Guid companyId)
        {
            if (User.Identity?.IsAuthenticated != true ||
                !await _context.HousingCompanies.AnyAsync(c => c.CompanyId == companyId))
            {
                return false;
            }
            var access = await HttpContext.AccessAsync();
            return access.IsPlatform && access.HasAny(Perm.CompaniesCreate, Perm.UsersManage);
        }

        private async Task AssignCompanySuperAdminAsync(AppUser user, Guid companyId)
        {
            var role = await _context.AccessRoles.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.IsSystem && r.Name == PermissionCatalog.CompanySuperAdminRoleName)
                       ?? AccessControlSeeder.NewCompanySuperAdmin(_context, companyId);
            _context.UserAccessRoles.Add(new RadiantHomes.Models.Security.UserAccessRole { UserId = user.Id, RoleId = role.Id, AssignedBy = User.Identity?.Name, AssignedOn = DateTime.UtcNow });
            HttpContext.RequestServices.GetRequiredService<AuditService>().Add(AuditCategories.Users, "UserCreated",
                $"Company administrator {user.FullName ?? user.Email} ({user.Email}) created with role {role.Name}.", companyId, nameof(AppUser), user.Id);
            await _context.SaveChangesAsync();
            AccessControlService.Invalidate();
        }

        // Options for the AdminRegister company / customer pickers.
        private async Task LoadSelectListsAsync()
        {
            // Only platform staff reach this (AdminRegister needs companies.create).
            var companies = _context.HousingCompanies.AsQueryable();
            ViewData["Companies"] = new SelectList(await companies.OrderBy(c => c.Name).ToListAsync(), "CompanyId", "Name");

            // Approved profiles that don't have an account yet.
            var accounts = _context.Set<AppUser>();
            var customers = await _context.UserProfiles
                .Where(c => c.ProfileStatus.ToLower() == "approved" && !accounts.Any(u => u.CustomerId == c.Id))
                .OrderBy(c => c.FirstName).ThenBy(c => c.LastName)
                .Select(c => new { c.Id, c.FirstName, c.LastName })
                .ToListAsync();
            ViewData["Customers"] = new SelectList(
                customers.Select(c => new { c.Id, Name = $"{c.FirstName} {c.LastName}".Trim() }), "Id", "Name");
        }

        private AppUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<AppUser>();

            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                    $"Ensure that '{nameof(IdentityUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<AppUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<AppUser>)_userStore;
        }
    }
}
