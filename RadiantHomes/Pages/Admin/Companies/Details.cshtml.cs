using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Admin.Companies
{
    /// <summary>Review one company: details, staff, approval actions (with reasons) and history.</summary>
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly CompanyLifecycleService _lifecycle;

        public DetailsModel(ApplicationDbContext db, CompanyLifecycleService lifecycle)
        {
            _db = db;
            _lifecycle = lifecycle;
        }

        [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
        [BindProperty] public string? Reason { get; set; }

        public HousingCompany Company { get; private set; } = null!;
        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public List<CompanyAction> Actions { get; private set; } = new();
        public List<CompanyStatusChange> History { get; private set; } = new();
        public List<(string Id, string Name, string Email)> Staff { get; private set; } = new();
        public int Projects { get; private set; }
        public int Listed { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            return await LoadAsync() ? Page() : NotFound();
        }

        public Task<IActionResult> OnPostApproveAsync() => Act(CompanyAction.Approve);
        public Task<IActionResult> OnPostRejectAsync() => Act(CompanyAction.Reject);
        public Task<IActionResult> OnPostRequestInfoAsync() => Act(CompanyAction.RequestInfo);
        public Task<IActionResult> OnPostSuspendAsync() => Act(CompanyAction.Suspend);
        public Task<IActionResult> OnPostReactivateAsync() => Act(CompanyAction.Reactivate);

        private async Task<IActionResult> Act(CompanyAction action)
        {
            var access = await HttpContext.AccessAsync();
            var result = await _lifecycle.ChangeAsync(access, Id, action, Reason);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { id = Id });
        }

        private async Task<bool> LoadAsync()
        {
            Access = await HttpContext.AccessAsync();
            var company = await _db.HousingCompanies.AsNoTracking().Include(c => c.Address).FirstOrDefaultAsync(c => c.CompanyId == Id);
            if (company is null) return false;
            Company = company;
            Actions = CompanyLifecycleService.AvailableActions(Access, Id, company.Status).ToList();
            History = await _lifecycle.HistoryAsync(Id);
            var staff = await _db.AppUsers.AsNoTracking().Include(u => u.Customer)
                .Where(u => u.HousingCompanyId == Id && u.AccountType == AccountType.CompanyStaff).ToListAsync();
            Staff = staff.Select(u => (u.Id, AccessManagementService.DisplayName(u), u.Email ?? string.Empty)).OrderBy(s => s.Item2).ToList();
            Projects = await _db.Projects.CountAsync(p => p.HousingCompanyId == Id);
            Listed = await _db.ApartmentHouses.CountAsync(a => a.Building!.Project!.HousingCompanyId == Id && a.IsAvailable && a.Status == "Approved");
            return true;
        }
    }
}
