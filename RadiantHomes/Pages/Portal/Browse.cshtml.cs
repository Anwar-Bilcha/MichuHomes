using Microsoft.AspNetCore.Mvc;
using RadiantHomes.Services.CustomerPortal;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>All listed homes with filters, or "browse by" groups: sites, bedrooms or real estate companies.</summary>
    public class BrowseModel : PortalPageModel
    {
        public const int PageSize = 12;

        /// <summary>null/"all" = homes; "sites", "bedrooms" or "companies" = groups.</summary>
        [BindProperty(SupportsGet = true)] public string? View { get; set; }
        [BindProperty(SupportsGet = true)] public Guid? Site { get; set; }
        [BindProperty(SupportsGet = true)] public string? Area { get; set; }
        [BindProperty(SupportsGet = true)] public int? Bedrooms { get; set; }
        [BindProperty(SupportsGet = true)] public Guid? Company { get; set; }
        [BindProperty(SupportsGet = true)] public double? MaxPrice { get; set; }
        [BindProperty(SupportsGet = true)] public string? Q { get; set; }
        [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
        /// <summary>Page number ("page" is taken by Razor Pages routing).</summary>
        [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

        public List<ApartmentCard> Items { get; private set; } = new();
        public int Total { get; private set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
        public List<ListingGroup> Groups { get; private set; } = new();

        public List<ListingGroup> SiteOptions { get; private set; } = new();
        public List<ListingGroup> CompanyOptions { get; private set; } = new();
        public List<string> AreaOptions { get; private set; } = new();

        public bool IsGroupView => View is "sites" or "bedrooms" or "companies";
        public int ActiveFilterCount => new object?[] { Site, string.IsNullOrWhiteSpace(Area) ? null : Area, Bedrooms, Company, MaxPrice is > 0 ? MaxPrice : null }.Count(v => v != null);

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            var catalog = Service<ApartmentCatalog>();

            switch (View)
            {
                case "sites": Groups = await catalog.SitesAsync(); return Page();
                case "bedrooms": Groups = await catalog.BedroomGroupsAsync(); return Page();
                case "companies": Groups = await catalog.CompaniesAsync(); return Page();
            }

            View = null;
            PageNumber = Math.Max(1, PageNumber);
            var filter = new ListingFilter { Site = Site, Area = Area, Bedrooms = Bedrooms, Company = Company, MaxPrice = MaxPrice, Q = Q, Sort = Sort };
            (Items, Total) = await catalog.SearchAsync(filter, PageNumber, PageSize);
            SiteOptions = await catalog.SitesAsync();
            CompanyOptions = await catalog.CompaniesAsync();
            AreaOptions = await catalog.AreasAsync();
            return Page();
        }

        /// <summary>Query values of the current filters, with some changed (null removes one).</summary>
        public Dictionary<string, string> Route(params (string Key, object? Value)[] changes)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Set(string key, object? value)
            {
                if (value is null || (value is string s && string.IsNullOrWhiteSpace(s))) values.Remove(key);
                else values[key] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;
            }
            Set("site", Site);
            Set("area", Area);
            Set("bedrooms", Bedrooms);
            Set("company", Company);
            Set("maxPrice", MaxPrice is > 0 ? MaxPrice : null);
            Set("q", Q);
            Set("sort", Sort);
            foreach (var (key, value) in changes) Set(key, value);
            return values;
        }

        public string? SiteName => Site is Guid id ? SiteOptions.FirstOrDefault(s => s.Key == id.ToString())?.Name : null;
        public string? CompanyName => Company is Guid id ? CompanyOptions.FirstOrDefault(c => c.Key == id.ToString())?.Name : null;
    }
}
