using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages
{
    /// <summary>
    /// Receives the footer "Stay updated" form from every public page and stores the email
    /// in Subscriptions, then sends the visitor back to the page they came from.
    /// </summary>
    public class SubscribeModel : PageModel
    {
        public const string MessageKey = "SubscribeMessage";
        public const string StatusKey = "SubscribeStatus";

        private readonly ApplicationDbContext _context;

        public SubscribeModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string? ReturnUrl { get; set; }

        public IActionResult OnGet() => RedirectToPage("/Index");

        public async Task<IActionResult> OnPostAsync()
        {
            var back = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : Url.Content("~/");

            if (!ModelState.IsValid)
            {
                TempData[StatusKey] = "danger";
                TempData[MessageKey] = "Please enter a valid email address.";
                return LocalRedirect(back);
            }

            var email = Email.Trim();
            var alreadySubscribed = await _context.Subscriptions
                .AnyAsync(s => s.Email.ToLower() == email.ToLower());

            if (!alreadySubscribed)
            {
                _context.Subscriptions.Add(new Subscriber
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    SubscriptionType = SubscriptionTypes.Daily.ToString()
                });
                await _context.SaveChangesAsync();
            }

            TempData[StatusKey] = "success";
            TempData[MessageKey] = alreadySubscribed
                ? "You're already subscribed — thanks!"
                : "Thanks! You're subscribed to Michu Homes updates.";
            return LocalRedirect(back);
        }
    }
}
