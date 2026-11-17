using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.Properties;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Uploads
{
    /// <summary>
    /// Apartment uploads and approvals. Platform reviewers (houses.approve) see every company's upload batches and the
    /// apartments created one at a time that wait for approval; company staff see their own uploads.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly BulkUploadService _uploads;

        public IndexModel(ApplicationDbContext db, BulkUploadService uploads)
        {
            _db = db;
            _uploads = uploads;
        }

        [BindProperty(SupportsGet = true)] public string? Status { get; set; }
        [BindProperty] public string? Reason { get; set; }

        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public string Current => string.IsNullOrEmpty(Status) ? UploadBatchStatuses.PendingReview : Status;
        public Dictionary<string, int> Counts { get; private set; } = new();
        public List<BatchRow> Batches { get; private set; } = new();
        public List<SingleRow> Singles { get; private set; } = new();
        public bool CanReview => Access.IsPlatform && Access.Has(Perm.HousesApprove);

        public sealed record BatchRow(Guid Id, string Reference, string Company, string Building, int Rows, int Pending, int Rejected, string Status, DateTime UploadedOn, string? UploadedBy);
        public sealed record SingleRow(Guid Id, string Title, string? Number, string Company, string Building, double Price, string? Currency, DateTime CreatedOn);

        public async Task OnGetAsync()
        {
            Access = await HttpContext.AccessAsync();
            var batches = Scoped();
            Counts = await batches.GroupBy(b => b.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N);
            if (Current != "all") batches = batches.Where(b => b.Status == Current);
            Batches = await batches.OrderByDescending(b => b.UploadedOn).Take(200)
                .Select(b => new BatchRow(b.Id, b.Reference, b.Company.Name, b.Building.BuildingName, b.Rows.Count,
                    b.Rows.Count(r => r.Status == UploadRowStatuses.Pending), b.Rows.Count(r => r.Status == UploadRowStatuses.Rejected),
                    b.Status, b.UploadedOn, b.UploadedByName))
                .ToListAsync();

            if (CanReview)
            {
                Singles = await _db.ApartmentHouses.AsNoTracking()
                    .Where(a => (a.Status == "Pending" || a.Status == "New") && !_db.UploadBatchRows.Any(r => r.ApartmentHouseId == a.Id))
                    .OrderBy(a => a.CreatedOn).Take(200)
                    .Select(a => new SingleRow(a.Id, a.Title, a.HouseNumber, a.Building!.Project!.Company!.Name, a.Building.BuildingName, a.SalePrice, a.Currency, a.CreatedOn))
                    .ToListAsync();
            }
        }

        public Task<IActionResult> OnPostApproveSingleAsync(Guid id) => Single(id, true);
        public Task<IActionResult> OnPostRejectSingleAsync(Guid id) => Single(id, false);

        private async Task<IActionResult> Single(Guid id, bool approve)
        {
            var result = await _uploads.DecideSingleAsync(await HttpContext.AccessAsync(), id, approve, Reason);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { status = Status });
        }

        private IQueryable<UploadBatch> Scoped()
        {
            var query = _db.UploadBatches.AsNoTracking();
            if (Access.IsPlatform) return query;
            var own = Access.CompanyId ?? Guid.Empty;
            return query.Where(b => b.CompanyId == own);
        }

        public int Count(string status) => Counts.TryGetValue(status, out var n) ? n : 0;
    }
}
