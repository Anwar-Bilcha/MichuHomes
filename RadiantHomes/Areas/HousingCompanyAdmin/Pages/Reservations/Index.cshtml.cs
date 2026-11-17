using Microsoft.AspNetCore.Mvc;
using RadiantHomes.Services.Security;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Services.CustomerPortal;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.Reservations
{
    /// <summary>
    /// Customers' reservations of the company's apartments: approve, mark as sold or cancel (each notifies the
    /// customer in their portal). Michu Admins see every company's reservations.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ReservationService _reservations;

        public IndexModel(ApplicationDbContext context, ReservationService reservations)
        {
            _context = context;
            _reservations = reservations;
        }

        /// <summary>"pending" (default), "approved", "completed", "cancelled" or "all".</summary>
        [BindProperty(SupportsGet = true)] public string? Status { get; set; }

        public List<ApartmentHouseSaleTransaction> Items { get; private set; } = new();
        public Dictionary<HouseReserveStatuses, int> Counts { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var scope = await CompanyScope.ForAsync(User, _context);
            var query = Scoped(scope);
            Counts = await query.GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);

            var filter = Filter;
            if (filter is HouseReserveStatuses status) query = query.Where(t => t.Status == status);
            Items = await query
                .Include(t => t.ApartmentHouse).ThenInclude(a => a.Building!).ThenInclude(b => b.Project!).ThenInclude(p => p.Company)
                .Include(t => t.Receiver)
                .OrderByDescending(t => t.TransactionDate)
                .Take(300)
                .ToListAsync();
            return Page();
        }

        public Task<IActionResult> OnPostApproveAsync(Guid id) => Change(id, HouseReserveStatuses.Approved);
        public Task<IActionResult> OnPostSoldAsync(Guid id) => Change(id, HouseReserveStatuses.Completed);
        public Task<IActionResult> OnPostCancelAsync(Guid id) => Change(id, HouseReserveStatuses.Cancelled);

        private async Task<IActionResult> Change(Guid id, HouseReserveStatuses to)
        {
            if (!(await HttpContext.AccessAsync()).Has(RadiantHomes.Services.Security.Perm.ReservationsManage))
            {
                TempData["Error"] = "You don't have permission to change reservations.";
                return RedirectToPage(new { status = Status });
            }
            var scope = await CompanyScope.ForAsync(User, _context);
            var result = await _reservations.ChangeStatusAsync(id, to, scope);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { status = Status });
        }

        public HouseReserveStatuses? Filter => Status switch
        {
            "approved" => HouseReserveStatuses.Approved,
            "completed" => HouseReserveStatuses.Completed,
            "cancelled" => HouseReserveStatuses.Cancelled,
            "all" => null,
            _ => HouseReserveStatuses.Pending,
        };

        public int Count(HouseReserveStatuses status) => Counts.TryGetValue(status, out var n) ? n : 0;

        // A Real Estate Admin sees their own company's apartments only (no company: nothing).
        private IQueryable<ApartmentHouseSaleTransaction> Scoped(CompanyScope scope)
        {
            var query = _context.ApartmentHouseSaleTransactions.AsNoTracking();
            if (!scope.IsRestricted) return query;
            if (scope.CompanyId is not Guid companyId) return query.Where(_ => false);
            return query.Where(t => t.ApartmentHouse.Building!.Project!.HousingCompanyId == companyId);
        }
    }
}
