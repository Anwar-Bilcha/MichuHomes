using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RadiantHomes.Utilities
{
    public sealed record Breadcrumb(string Text, string? Url);

    /// <summary>
    /// Builds the shared page chrome (title + breadcrumbs) used by every layout,
    /// so deep pages get a consistent header without repeating markup.
    /// </summary>
    public static partial class PageChrome
    {
        public const string BrandName = "Michu Homes";

        private static readonly HashSet<string> ScaffoldPageNames =
            new(StringComparer.OrdinalIgnoreCase) { "Index", "Create", "Edit", "Delete", "Details" };

        private static readonly Dictionary<string, string> SegmentNames =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["HousingCompanyAdmin"] = "Company Admin",
                ["OnBoarding"] = "Onboarding",
                ["ApartmentHouses"] = "Apartments",
                ["RealEstates"] = "Real Estates",
                ["Identity"] = "Account",
                ["LiveVideoMeet"] = "Video call with an agent",
            };

        private static readonly object CacheLock = new();
        private static int _cachedVersion = -1;
        private static HashSet<string> _pagePaths = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Splits PascalCase route segments into words ("ApartmentHouses" → "Apartment Houses").</summary>
        public static string Humanize(string segment)
        {
            if (SegmentNames.TryGetValue(segment, out var known)) return known;
            return WordBoundary().Replace(segment, " $1").Trim();
        }

        private static string Singular(string words)
        {
            if (words.EndsWith("ies", StringComparison.OrdinalIgnoreCase)) return words[..^3] + "y";
            if (words.EndsWith("ss", StringComparison.OrdinalIgnoreCase)) return words;
            if (words.EndsWith("s", StringComparison.OrdinalIgnoreCase)) return words[..^1];
            return words;
        }

        private static string Plural(string words) =>
            words.EndsWith("s", StringComparison.OrdinalIgnoreCase) ? words : words + "s";

        /// <summary>
        /// Returns the page's own title, or a readable one derived from its route when the page
        /// still uses a scaffolded title such as "Index" or "Details".
        /// </summary>
        public static string ResolveTitle(string? title, string? pagePath, string? area = null)
        {
            if (!string.IsNullOrWhiteSpace(title) && !ScaffoldPageNames.Contains(title.Trim()))
                return title.Trim();

            var segments = (pagePath ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return title?.Trim() is { Length: > 0 } t ? t : BrandName;

            var pageName = segments[^1];
            var folder = segments.Length > 1
                ? Humanize(segments[^2])
                : !string.IsNullOrEmpty(area) ? Humanize(area) : BrandName;
            var entity = Singular(folder);

            return pageName.ToLowerInvariant() switch
            {
                "index" => segments.Length > 1 ? Plural(folder) : folder,
                "create" => $"New {entity.ToLowerInvariant()}",
                "edit" => $"Edit {entity.ToLowerInvariant()}",
                "delete" => $"Delete {entity.ToLowerInvariant()}",
                "details" => $"{entity} details",
                _ => Humanize(pageName),
            };
        }

        /// <summary>
        /// Breadcrumb trail for the current page: Home › folders… › current title.
        /// Folder crumbs link only when an Index page exists for that folder.
        /// </summary>
        public static IReadOnlyList<Breadcrumb> Breadcrumbs(
            ViewContext context, IActionDescriptorCollectionProvider provider, IUrlHelper url, string currentTitle)
        {
            var area = context.RouteData.Values["area"]?.ToString() ?? string.Empty;
            var page = context.RouteData.Values["page"]?.ToString() ?? string.Empty;
            var segments = page.Split('/', StringSplitOptions.RemoveEmptyEntries);

            var crumbs = new List<Breadcrumb> { new("Home", url.Page("/Index", new { area = "" })) };
            if (segments.Length == 0 || (segments.Length == 1 && area.Length == 0 && segments[0] == "Index"))
                return crumbs;

            var known = KnownPages(provider);
            if (area.Length > 0)
            {
                // Area roots are shown as context only (the HousingCompanyAdmin root route is ambiguous).
                crumbs.Add(new(Humanize(area), null));
            }

            // Folders between root and the page itself. For an Index page the last folder is the page.
            var folderCount = segments.Length - 1;
            if (segments[^1].Equals("Index", StringComparison.OrdinalIgnoreCase)) folderCount--;

            // Folders without an Index page are code organisation (e.g. "Actions", "Admin"), not
            // places a user can go, so they are left out of the trail.
            for (var i = 0; i < folderCount; i++)
            {
                var indexPath = "/" + string.Join('/', segments.Take(i + 1)) + "/Index";
                if (known.Contains($"{area}:{indexPath}"))
                {
                    crumbs.Add(new(Humanize(segments[i]), url.Page(indexPath, new { area })));
                }
            }

            crumbs.Add(new(currentTitle, null));
            return crumbs;
        }

        private static HashSet<string> KnownPages(IActionDescriptorCollectionProvider provider)
        {
            var collection = provider.ActionDescriptors;
            if (collection.Version == _cachedVersion) return _pagePaths;

            lock (CacheLock)
            {
                if (collection.Version != _cachedVersion)
                {
                    _pagePaths = collection.Items
                        .OfType<PageActionDescriptor>()
                        .Select(d => $"{d.AreaName ?? string.Empty}:{d.ViewEnginePath}")
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    _cachedVersion = collection.Version;
                }
            }
            return _pagePaths;
        }

        [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
        private static partial Regex WordBoundary();
    }
}
