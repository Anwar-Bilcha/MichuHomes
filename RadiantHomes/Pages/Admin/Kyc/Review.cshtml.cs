using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Kyc;
using RadiantHomes.Services.Kyc;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Admin.Kyc
{
    /// <summary>One identity verification application: details, documents (each opening audited), decisions, history.</summary>
    public class ReviewModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly KycService _kyc;

        public ReviewModel(ApplicationDbContext db, KycService kyc)
        {
            _db = db;
            _kyc = kyc;
        }

        [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
        [BindProperty] public string? Reason { get; set; }

        public KycApplication Application { get; private set; } = null!;
        public List<KycAction> Actions { get; private set; } = new();
        public int EarlierApplications { get; private set; }
        public bool IsLatest { get; private set; }

        public async Task<IActionResult> OnGetAsync() => await LoadAsync() ? Page() : NotFound();

        public Task<IActionResult> OnPostStartReviewAsync() => Act(KycAction.StartReview);
        public Task<IActionResult> OnPostApproveAsync() => Act(KycAction.Approve);
        public Task<IActionResult> OnPostRejectAsync() => Act(KycAction.Reject);
        public Task<IActionResult> OnPostRequestResubmissionAsync() => Act(KycAction.RequestResubmission);
        public Task<IActionResult> OnPostRevokeAsync() => Act(KycAction.Revoke);

        public async Task<IActionResult> OnGetDocumentAsync(Guid documentId)
        {
            var file = await _kyc.OpenDocumentAsync(await HttpContext.AccessAsync(), documentId, null);
            if (file is null) return NotFound();
            Response.Headers.CacheControl = "no-store";
            return PhysicalFile(file.Value.Path, file.Value.ContentType);
        }

        private async Task<IActionResult> Act(KycAction action)
        {
            var access = await HttpContext.AccessAsync();
            var result = await _kyc.DecideAsync(access, Id, action, Reason, User.Identity?.Name);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { id = Id });
        }

        private async Task<bool> LoadAsync()
        {
            var application = await _db.KycApplications.AsNoTracking()
                .Include(a => a.Customer).Include(a => a.Documents).Include(a => a.History)
                .FirstOrDefaultAsync(a => a.Id == Id);
            if (application is null) return false;
            Application = application;
            IsLatest = !await _db.KycApplications.AnyAsync(a => a.CustomerId == application.CustomerId && a.SubmittedOn > application.SubmittedOn);
            EarlierApplications = await _db.KycApplications.CountAsync(a => a.CustomerId == application.CustomerId && a.SubmittedOn < application.SubmittedOn);
            Actions = IsLatest ? KycService.Available(await HttpContext.AccessAsync(), application.Status).ToList() : new();
            return true;
        }
    }
}
