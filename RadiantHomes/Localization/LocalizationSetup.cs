using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace RadiantHomes.Localization
{
    public sealed record SiteLanguage(string Code, string NativeName);

    /// <summary>Wiring for the site's languages: culture selection, page translation and the language switch.</summary>
    public static class LocalizationSetup
    {
        /// <summary>Languages offered in the language picker (English is the source language).</summary>
        public static readonly IReadOnlyList<SiteLanguage> Languages = new[]
        {
            new SiteLanguage("en", "English"),
            new SiteLanguage("am", "አማርኛ"),
        };

        public static IServiceCollection AddMichuLocalization(this IServiceCollection services)
        {
            services.AddSingleton<TranslationCatalog>();
            services.AddSingleton<MissingTranslations>();
            services.Configure<RequestLocalizationOptions>(options =>
            {
                var cultures = Languages.Select(l => new CultureInfo(l.Code)).ToList();
                options.DefaultRequestCulture = new RequestCulture("en");
                options.SupportedCultures = cultures;
                options.SupportedUICultures = cultures;
                // Order: ?culture=xx (handy for testing) → the language cookie → the browser's preferred language.
                options.FallBackToParentCultures = true;
                options.FallBackToParentUICultures = true;
            });
            return services;
        }

        /// <summary>Call after UseStaticFiles and before UseRouting.</summary>
        public static WebApplication UseMichuLocalization(this WebApplication app)
        {
            app.UseRequestLocalization();
            app.UseMiddleware<HtmlTranslationMiddleware>();
            return app;
        }

        public static WebApplication MapMichuLocalizationEndpoints(this WebApplication app)
        {
            // /language/am?returnUrl=/ApartmentHouses — remembers the choice for a year and returns to the page.
            app.MapGet("/language/{code}", (string code, string? returnUrl, HttpContext context) =>
            {
                var language = Languages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) ?? Languages[0];
                context.Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(language.Code)),
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax, Path = "/" });

                var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\")
                    ? returnUrl : "/";
                return Results.LocalRedirect(target);
            });

            if (app.Environment.IsDevelopment())
            {
                // Texts rendered without a translation since the app started (crawl pages in a language first).
                app.MapGet("/_dev/translations/missing/{culture}", (string culture, MissingTranslations missing) => Results.Json(missing.Report(culture)));
                app.MapPost("/_dev/translations/missing/clear", (MissingTranslations missing) => { missing.Clear(); return Results.Ok(); })
                   .DisableAntiforgery();
            }
            return app;
        }
    }
}
