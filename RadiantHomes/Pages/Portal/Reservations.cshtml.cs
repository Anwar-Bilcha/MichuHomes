using Microsoft.AspNetCore.Mvc;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.CustomerPortal;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>The customer's reservations: active (waiting or approved) and past (cancelled or closed).</summary>
    public class ReservationsModel : PortalPageModel
    {
        /// <summary>"past" shows cancelled and closed reservations; anything else the active ones.</summary>
        [BindProperty(SupportsGet = true)] public string? Show { get; set; }

        public bool ShowPast => Show == "past";
        public List<ApartmentHouseSaleTransaction> Items { get; private set; } = new();
        public Dictionary<Guid, string?> Photos { get; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.CustomerId is not Guid customerId) return Page();

            var statuses = ShowPast
                ? new[] { HouseReserveStatuses.Cancelled, HouseReserveStatuses.Closed, HouseReserveStatuses.Rejected, HouseReserveStatuses.Expired, HouseReserveStatuses.Reversed }
                : ReservationService.Active;
            Items = await Service<ReservationService>().ForCustomerAsync(customerId, statuses);
            var cards = Service<ApartmentCatalog>().ToCards(Items.Select(t => t.ApartmentHouse).Where(a => a != null));
            foreach (var card in cards) Photos[card.Id] = card.PhotoUrl;
            return Page();
        }

        public async Task<IActionResult> OnPostCancelAsync(Guid id)
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.CustomerId is not Guid customerId) return CompleteProfileFirst();

            var result = await Service<ReservationService>().CancelByCustomerAsync(id, customerId);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage();
        }
    }
}
