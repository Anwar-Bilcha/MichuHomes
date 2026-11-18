using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.Services.Security;
using RadiantHomes.Services.Settings;

namespace RadiantHomes.Pages.Admin.Settings
{
    /// <summary>Platform-wide settings (settings.manage). Changes are audited.</summary>
    public class IndexModel : PageModel
    {
        private readonly PlatformSettings _settings;
        public IndexModel(PlatformSettings settings) => _settings = settings;

        [BindProperty] public Dictionary<string, int> Values { get; set; } = new();

        public async Task OnGetAsync()
        {
            foreach (var d in SettingKeys.All) Values[d.Key] = await _settings.GetIntAsync(d.Key);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var access = await HttpContext.AccessAsync();
            var messages = new List<string>();
            var failed = false;
            foreach (var d in SettingKeys.All)
            {
                if (!Values.TryGetValue(d.Key, out var value)) continue;
                if (await _settings.GetIntAsync(d.Key) == value) continue;
                var result = await _settings.SetIntAsync(access, d.Key, value);
                messages.Add(result.Message);
                failed |= !result.Succeeded;
            }
            TempData[failed ? "Error" : "Success"] = messages.Count == 0 ? "Nothing changed." : string.Join(" ", messages);
            return RedirectToPage();
        }
    }
}
