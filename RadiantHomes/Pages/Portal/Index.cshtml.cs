using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.CustomerPortal;

namespace RadiantHomes.Pages.Portal
{
    public class IndexModel : PortalPageModel
    {
        public List<ApartmentCard> Latest { get; private set; } = new();
        public List<ListingGroup> Bedrooms { get; private set; } = new();
        public List<ListingGroup> Sites { get; private set; } = new();
        public int ActiveReservations { get; private set; }
        public int Bought { get; private set; }
        public int Friends { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;

            var catalog = Service<ApartmentCatalog>();
            Latest = await catalog.LatestAsync(10);
            Bedrooms = await catalog.BedroomGroupsAsync();
            Sites = (await catalog.SitesAsync()).OrderByDescending(s => s.Count).Take(10).ToList();

            if (Me.CustomerId is Guid id)
            {
                var context = Service<ApplicationDbContext>();
                ActiveReservations = await context.ApartmentHouseSaleTransactions
                    .CountAsync(t => t.CustomerId == id && (t.Status == HouseReserveStatuses.Pending || t.Status == HouseReserveStatuses.Approved));
                Bought = await context.ApartmentHouseSaleTransactions
                    .CountAsync(t => t.CustomerId == id && t.Status == HouseReserveStatuses.Completed);
                Friends = await context.Referrals.CountAsync(r => r.ReferralCode!.CustomerId == id);
            }
            return Page();
        }
    }
}
