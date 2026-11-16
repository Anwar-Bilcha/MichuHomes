using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.Services.CustomerPortal;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>
    /// Base of the customer portal pages (the /Portal folder requires sign-in, see Program.cs). Loads the signed-in
    /// user, their customer profile and unread notifications, which the portal layout shows.
    /// </summary>
    public abstract class PortalPageModel : PageModel
    {
        public PortalUser Me { get; private set; } = default!;
        public int UnreadNotifications { get; private set; }

        protected T Service<T>() where T : notnull => HttpContext.RequestServices.GetRequiredService<T>();

        /// <summary>Null when the user is loaded; otherwise the result to return (the account no longer exists).</summary>
        protected async Task<IActionResult?> LoadMeAsync()
        {
            var me = await Service<PortalUsers>().GetAsync(User);
            if (me is null) return Challenge();
            Me = me;
            if (me.CustomerId is Guid id) UnreadNotifications = await Service<CustomerNotifications>().UnreadCountAsync(id);
            return null;
        }

        /// <summary>Sends the user to Portal/Profile, coming back here afterwards.</summary>
        protected IActionResult CompleteProfileFirst(string? returnUrl = null)
        {
            TempData["Info"] = "Please complete your profile first.";
            return RedirectToPage("/Portal/Profile", new { returnUrl = returnUrl ?? CurrentUrl });
        }

        protected string CurrentUrl => Request.Path + Request.QueryString;
    }
}
