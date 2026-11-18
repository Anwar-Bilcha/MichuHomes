using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Services.CustomerPortal;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Sales
{
    /// <summary>
    /// Sales: approved reservations ready to be recorded, recorded sales (price, discount, agent, payment, agreement)
    /// with payment updates, and reversal with a reason. Agents see the sales of their projects and their own.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AgentService _agents;
        private readonly SalesReports _reports;
        private readonly ReservationService _reservations;
        private readonly SaleAgreements _agreements;
        private readonly AuditService _audit;

        public IndexModel(ApplicationDbContext db, AgentService agents, SalesReports reports, ReservationService reservations, SaleAgreements agreements, AuditService audit)
        {
            _db = db;
            _agents = agents;
            _reports = reports;
            _reservations = reservations;
            _agreements = agreements;
            _audit = audit;
        }

        /// <summary>"sold" (default), "ready" (approved reservations), "reversed" or "all".</summary>
        [BindProperty(SupportsGet = true)] public string? Show { get; set; }
        [BindProperty] public string? PaymentStatus { get; set; }
        [BindProperty] public string? Reason { get; set; }

        public string Current => string.IsNullOrEmpty(Show) ? "sold" : Show;
        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public List<ApartmentHouseSaleTransaction> Items { get; private set; } = new();
        public Dictionary<string, string> Names { get; private set; } = new();
        public Dictionary<string, int> Counts { get; private set; } = new();
        public double SoldValue { get; private set; }

        public async Task OnGetAsync()
        {
            Access = await HttpContext.AccessAsync();
            var scope = await _agents.ScopeAsync(Access);
            var all = _reports.ScopedSales(scope);
            Counts = new()
            {
                ["sold"] = await all.CountAsync(t => t.Status == HouseReserveStatuses.Completed),
                ["ready"] = await all.CountAsync(t => t.Status == HouseReserveStatuses.Approved),
                ["reversed"] = await all.CountAsync(t => t.Status == HouseReserveStatuses.Reversed),
            };
            SoldValue = await all.Where(t => t.Status == HouseReserveStatuses.Completed).SumAsync(t => t.AgreedPrice ?? 0);
            var q = Current switch
            {
                "ready" => all.Where(t => t.Status == HouseReserveStatuses.Approved),
                "reversed" => all.Where(t => t.Status == HouseReserveStatuses.Reversed),
                "all" => all.Where(t => t.Status == HouseReserveStatuses.Completed || t.Status == HouseReserveStatuses.Reversed || t.Status == HouseReserveStatuses.Approved),
                _ => all.Where(t => t.Status == HouseReserveStatuses.Completed),
            };
            Items = await q.Include(t => t.ApartmentHouse).ThenInclude(a => a.Building!).ThenInclude(b => b.Project!).ThenInclude(p => p.Company)
                .Include(t => t.Receiver)
                .OrderByDescending(t => t.SoldOn ?? t.TransactionDate).Take(300).AsSplitQuery().ToListAsync();
            Names = await _reports.NamesAsync(Items.Where(t => t.SoldByUserId != null).Select(t => t.SoldByUserId!));
        }

        public async Task<IActionResult> OnPostPaymentAsync(Guid id)
        {
            var access = await HttpContext.AccessAsync();
            if (!access.Has(Perm.SalesRecord) || !await VisibleAsync(access, id)) return Done(AccessResult.Fail("You can't update this sale."));
            var result = await _reservations.SetPaymentStatusAsync(id, await CompanyScope.ForAsync(User, _db), PaymentStatus ?? string.Empty, access.UserId, User.Identity?.Name);
            return Done(new AccessResult(result.Succeeded, result.Message));
        }

        public async Task<IActionResult> OnPostReverseAsync(Guid id)
        {
            var access = await HttpContext.AccessAsync();
            if (!access.Has(Perm.SalesReverse) || !await VisibleAsync(access, id)) return Done(AccessResult.Fail("You can't reverse this sale."));
            var result = await _reservations.ReverseSaleAsync(id, await CompanyScope.ForAsync(User, _db), Reason, access.UserId, User.Identity?.Name);
            if (result.Succeeded)
            {
                var t = await _db.ApartmentHouseSaleTransactions.AsNoTracking().FirstAsync(x => x.Id == id);
                _audit.Add(AuditCategories.Sales, "SaleReversed", $"Sale {t.Reference} reversed: {t.ReversalReason}", t.CompanyId, nameof(ApartmentHouseSaleTransaction), t.Id.ToString());
                await _db.SaveChangesAsync();
            }
            return Done(new AccessResult(result.Succeeded, result.Message));
        }

        /// <summary>Opens the signed agreement (audited).</summary>
        public async Task<IActionResult> OnGetAgreementAsync(Guid id)
        {
            var access = await HttpContext.AccessAsync();
            if (!access.HasAny(Perm.SalesRecord, Perm.SalesReverse) || !await VisibleAsync(access, id)) return NotFound();
            var t = await _db.ApartmentHouseSaleTransactions.AsNoTracking().FirstAsync(x => x.Id == id);
            if (_agreements.Find(t.Id, t.AgreementFileName) is not { } file) return NotFound();
            _audit.Add(AuditCategories.Sales, "AgreementOpened", $"Agreement of sale {t.Reference} opened.", t.CompanyId, nameof(ApartmentHouseSaleTransaction), t.Id.ToString());
            await _db.SaveChangesAsync();
            return PhysicalFile(file.Path, file.ContentType, $"agreement-{t.Reference}{Path.GetExtension(file.Path)}");
        }

        private async Task<bool> VisibleAsync(UserAccess access, Guid id) =>
            await _reports.ScopedSales(await _agents.ScopeAsync(access)).AnyAsync(t => t.Id == id);

        private IActionResult Done(AccessResult result)
        {
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { show = Show });
        }

        public int Count(string key) => Counts.TryGetValue(key, out var n) ? n : 0;
    }
}
