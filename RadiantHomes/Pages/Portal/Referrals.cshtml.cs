using Microsoft.AspNetCore.Mvc;
using RadiantHomes.Models;
using RadiantHomes.Services.CustomerPortal;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>"Refer a friend": the customer's referral codes, sharing, and the friends who joined.</summary>
    public class ReferralsModel : PortalPageModel
    {
        public List<ReferralCode> Codes { get; private set; } = new();
        public List<ReferredFriend> Friends { get; private set; } = new();
        public int ActiveCount => Codes.Count(c => c.IsActive);

        [BindProperty] public string? Label { get; set; }

        /// <summary>The full link a friend opens (short: /r/{code}).</summary>
        public string LinkFor(string code) => $"{Request.Scheme}://{Request.Host}{ReferralService.LinkPath(code)}";

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.CustomerId is Guid customerId)
            {
                var referrals = Service<ReferralService>();
                Codes = await referrals.CodesAsync(customerId);
                Friends = await referrals.FriendsAsync(customerId);
            }
            return Page();
        }

        public async Task<IActionResult> OnPostGenerateAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.CustomerId is not Guid customerId) return CompleteProfileFirst("/Portal/Referrals");

            var (code, error) = await Service<ReferralService>().GenerateAsync(customerId, Label);
            if (code is null) TempData["Error"] = error;
            else TempData["Success"] = $"Your new code {code.Code} is ready to share.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleAsync(Guid id, bool active)
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.CustomerId is not Guid customerId) return CompleteProfileFirst("/Portal/Referrals");

            var changed = await Service<ReferralService>().SetActiveAsync(id, customerId, active);
            if (!changed)
                TempData["Error"] = active
                    ? $"You can have at most {ReferralService.MaxActiveCodes} active codes. Turn one off first."
                    : "This code could not be changed.";
            else
                TempData["Success"] = active ? "The code works again." : "The code is turned off. Friends can no longer join with it.";
            return RedirectToPage();
        }
    }
}
