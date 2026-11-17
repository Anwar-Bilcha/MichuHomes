using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages
{
    /// <summary>The company's own approval status, the platform's messages, and "send for approval again".</summary>
    public class StatusModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly CompanyLifecycleService _lifecycle;

        public StatusModel(ApplicationDbContext db, CompanyLifecycleService lifecycle)
        {
            _db = db;
            _lifecycle = lifecycle;
        }

        [BindProperty] public string? Note { get; set; }

        public HousingCompany? Company { get; private set; }
        public List<CompanyStatusChange> History { get; private set; } = new();
        public bool CanResubmit { get; private set; }
        public string? LatestReason => History.FirstOrDefault(h => !string.IsNullOrWhiteSpace(h.Reason) && h.ToStatus == Company?.Status)?.Reason;

        public async Task<IActionResult> OnGetAsync()
        {
            var access = await HttpContext.AccessAsync();
            if (access.IsPlatform) return RedirectToPage("/Admin/Companies/Index", new { area = "" });
            if (access.CompanyId is not Guid companyId) return Forbid();
            Company = await _db.HousingCompanies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (Company is null) return NotFound();
            History = await _lifecycle.HistoryAsync(companyId);
            CanResubmit = CompanyLifecycleService.AvailableActions(access, companyId, Company.Status).Contains(CompanyAction.Resubmit);
            return Page();
        }

        public async Task<IActionResult> OnPostResubmitAsync()
        {
            var access = await HttpContext.AccessAsync();
            if (access.CompanyId is not Guid companyId) return Forbid();
            var result = await _lifecycle.ChangeAsync(access, companyId, CompanyAction.Resubmit, Note);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage();
        }
    }
}
