using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.Properties;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Uploads
{
    /// <summary>One upload batch: every apartment with its decision. Reviewers decide rows; the company resubmits rejected ones.</summary>
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly BulkUploadService _uploads;

        public DetailsModel(ApplicationDbContext db, BulkUploadService uploads)
        {
            _db = db;
            _uploads = uploads;
        }

        [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
        [BindProperty] public List<Guid> RowIds { get; set; } = new();
        [BindProperty] public string? Reason { get; set; }

        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public UploadBatch Batch { get; private set; } = null!;
        public bool CanReview => Access.IsPlatform && Access.Has(Perm.HousesApprove);
        public bool CanResubmit => Access.Can(Perm.HousesUpdate, Batch.CompanyId);

        public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

        public async Task<IActionResult> OnPostApproveAsync() => await Decide(approve: true);
        public async Task<IActionResult> OnPostRejectAsync() => await Decide(approve: false);

        public async Task<IActionResult> OnPostApproveAllAsync()
        {
            if (!await LoadAsync()) return NotFound();
            RowIds = Batch.Rows.Where(r => r.Status == UploadRowStatuses.Pending).Select(r => r.Id).ToList();
            return await Decide(approve: true);
        }

        public async Task<IActionResult> OnPostResubmitAsync(Guid rowId)
        {
            var result = await _uploads.ResubmitAsync(await HttpContext.AccessAsync(), rowId);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { id = Id });
        }

        private async Task<IActionResult> Decide(bool approve)
        {
            var access = await HttpContext.AccessAsync();
            var result = await _uploads.DecideAsync(access, User.Identity?.Name, Id,
                approve ? RowIds : Array.Empty<Guid>(), approve ? Array.Empty<Guid>() : RowIds, Reason);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { id = Id });
        }

        private async Task<bool> LoadAsync()
        {
            Access = await HttpContext.AccessAsync();
            var batch = await _db.UploadBatches.AsNoTracking()
                .Include(b => b.Company).Include(b => b.Building)
                .Include(b => b.Rows).ThenInclude(r => r.ApartmentHouse)
                .FirstOrDefaultAsync(b => b.Id == Id);
            if (batch is null || !(Access.IsPlatform || (Access.IsCompanyStaff && Access.CompanyId == batch.CompanyId))) return false;
            Batch = batch;
            return true;
        }
    }
}
