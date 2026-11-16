using Microsoft.AspNetCore.Mvc;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>The "More" tab on phones: everything that isn't in the bottom bar.</summary>
    public class MoreModel : PortalPageModel
    {
        public async Task<IActionResult> OnGetAsync() => await LoadMeAsync() ?? Page();
    }
}
