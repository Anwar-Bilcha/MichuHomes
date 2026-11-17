using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.Models;
using RadiantHomes.Services.Email;

namespace RadiantHomes.Areas.Identity.Pages.Account
{
    /// <summary>Sends a password-reset link. The answer is the same whether or not the email belongs to an account.</summary>
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AccountEmails _emails;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(UserManager<AppUser> userManager, AccountEmails emails, ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _emails = emails;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            // Accounts that are deactivated (locked) get no link; nobody learns whether the address exists.
            if (user != null && !await _userManager.IsLockedOutAsync(user))
            {
                try
                {
                    await _emails.SendPasswordLinkAsync(user, invitation: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The password reset email could not be sent.");
                }
            }
            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}
