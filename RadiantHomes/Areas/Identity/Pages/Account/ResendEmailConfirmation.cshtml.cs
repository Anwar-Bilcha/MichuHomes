using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.Models;
using RadiantHomes.Services.Email;

namespace RadiantHomes.Areas.Identity.Pages.Account
{
    /// <summary>Sends the email-confirmation link again. Same answer whether or not the address has an account.</summary>
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AccountEmails _emails;
        private readonly ILogger<ResendEmailConfirmationModel> _logger;

        public ResendEmailConfirmationModel(UserManager<AppUser> userManager, AccountEmails emails, ILogger<ResendEmailConfirmationModel> logger)
        {
            _userManager = userManager;
            _emails = emails;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public bool Sent { get; private set; }

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
            if (user != null && !user.EmailConfirmed)
            {
                try
                {
                    await _emails.SendConfirmationAsync(user);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "The confirmation email could not be sent.");
                }
            }
            Sent = true;
            return Page();
        }
    }
}
