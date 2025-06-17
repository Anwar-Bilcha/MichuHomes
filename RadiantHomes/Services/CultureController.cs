using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace RadiantHomes.Services
{
    public class CultureController : Controller
    {
        [HttpPost]
        public IActionResult SetLanguage(string culture)
        {
            // Set the requested culture
            var cultureInfo = new CultureInfo(culture);

            // Set the culture for the current request
            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;

            // Save the culture to a cookie or session, so the user preference is retained
            Response.Cookies.Append(
                "Culture",
                culture,
                new CookieOptions { Expires = DateTime.UtcNow.AddYears(1) });

            // Redirect back to the previous page
            return Redirect(Request.Headers["Referer"].ToString());
        }
    }

}
