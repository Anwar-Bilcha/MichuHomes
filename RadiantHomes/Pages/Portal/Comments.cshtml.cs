using Microsoft.AspNetCore.Mvc;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.CustomerPortal;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>The comments the customer wrote on homes.</summary>
    public class CommentsModel : PortalPageModel
    {
        public List<ApartmentHouseReview> Items { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.CustomerId is Guid customerId) Items = await Service<ApartmentComments>().ByCustomerAsync(customerId);
            return Page();
        }
    }
}
