using Microsoft.AspNetCore.Mvc;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.CustomerPortal;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>Homes the customer bought: reservations the company marked as sold.</summary>
    public class BoughtModel : PortalPageModel
    {
        public List<ApartmentHouseSaleTransaction> Items { get; private set; } = new();
        public Dictionary<Guid, string?> Photos { get; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.CustomerId is not Guid customerId) return Page();

            Items = await Service<ReservationService>().ForCustomerAsync(customerId, HouseReserveStatuses.Completed);
            foreach (var card in Service<ApartmentCatalog>().ToCards(Items.Select(t => t.ApartmentHouse).Where(a => a != null)))
                Photos[card.Id] = card.PhotoUrl;
            return Page();
        }
    }
}
