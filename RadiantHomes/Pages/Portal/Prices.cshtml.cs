using Microsoft.AspNetCore.Mvc;
using RadiantHomes.Services.CustomerPortal;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>The lowest asking price of each real estate company, for a number of bedrooms.</summary>
    public class PricesModel : PortalPageModel
    {
        /// <summary>Bedrooms to compare (null = all homes); 4 means 4 or more.</summary>
        [BindProperty(SupportsGet = true)] public int? Bedrooms { get; set; }

        public List<CompanyPrice> Rows { get; private set; } = new();
        public List<ListingGroup> BedroomGroups { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            var catalog = Service<ApartmentCatalog>();
            BedroomGroups = await catalog.BedroomGroupsAsync();
            Rows = await catalog.LowestPricesAsync(Bedrooms);
            return Page();
        }

        /// <summary>How much more than the best price of the same currency.</summary>
        public double AboveBest(CompanyPrice row) =>
            row.LowestPrice - Rows.Where(r => r.Currency == row.Currency).Min(r => r.LowestPrice);
    }
}
