using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Sales;
using RadiantHomes.Services.Sales;

namespace RadiantHomes.Pages.Portal.Visits
{
    /// <summary>The customer's site visits: upcoming (requested or confirmed) and past; cancel or ask for another time.</summary>
    public class IndexModel : PortalPageModel
    {
        [BindProperty(SupportsGet = true)] public string? Show { get; set; }
        [BindProperty] public string? Reason { get; set; }
        [BindProperty] public DateTime? Date { get; set; }
        [BindProperty] public string? Time { get; set; }

        public bool ShowPast => Show == "past";
        public List<SiteVisit> Items { get; private set; } = new();
        public Dictionary<string, string> Agents { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            var me = Me.Account.Id;
            var q = Service<ApplicationDbContext>().SiteVisits.AsNoTracking().Include(v => v.Project).Include(v => v.Company).Include(v => v.ApartmentHouse).Include(v => v.History)
                .Where(v => v.CustomerUserId == me);
            q = ShowPast ? q.Where(v => !SiteVisitStatuses.Open.Contains(v.Status)) : q.Where(v => SiteVisitStatuses.Open.Contains(v.Status));
            Items = await q.OrderByDescending(v => v.CreatedOn).Take(100).AsSplitQuery().ToListAsync();
            Agents = await Service<SalesReports>().NamesAsync(Items.Where(v => v.AgentUserId != null && v.Status == SiteVisitStatuses.Confirmed).Select(v => v.AgentUserId!));
            return Page();
        }

        public async Task<IActionResult> OnPostCancelAsync(Guid id)
        {
            if (await LoadMeAsync() is { } problem) return problem;
            var result = await Service<SiteVisitService>().CancelByCustomerAsync(Me.Account.Id, id, Reason);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMoveAsync(Guid id)
        {
            if (await LoadMeAsync() is { } problem) return problem;
            var result = Date is DateTime d
                ? await Service<SiteVisitService>().RequestNewTimeAsync(Me.Account.Id, id, d, Time ?? string.Empty, Reason)
                : RadiantHomes.Services.Security.AccessResult.Fail("Choose a day.");
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage();
        }
    }
}
