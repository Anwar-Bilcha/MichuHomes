using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.DAL;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Leads
{
    /// <summary>Add a lead by hand (walk-in, phone, campaign, referral…).</summary>
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly LeadService _leads;
        private readonly SalesReports _reports;

        public CreateModel(ApplicationDbContext db, LeadService leads, SalesReports reports)
        {
            _db = db;
            _leads = leads;
            _reports = reports;
        }

        [BindProperty] public LeadInput Input { get; set; } = new() { Source = "WalkIn" };
        public LeadFormOptions Options { get; private set; } = new();
        public string? Error { get; private set; }

        public async Task OnGetAsync()
        {
            var access = await HttpContext.AccessAsync();
            Options = await LeadFormOptions.LoadAsync(_db, _reports, access);
            if (!Options.CanAssign) Input.AgentUserId = access.UserId;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var access = await HttpContext.AccessAsync();
            Input.AgentUserId = string.IsNullOrWhiteSpace(Input.AgentUserId) ? null : Input.AgentUserId;
            var result = await _leads.CreateAsync(access, Input, User.Identity?.Name);
            if (!result.Succeeded)
            {
                Error = result.Message;
                Options = await LeadFormOptions.LoadAsync(_db, _reports, access);
                return Page();
            }
            TempData["Success"] = result.Message;
            return RedirectToPage("./Details", new { id = result.Id });
        }
    }
}
