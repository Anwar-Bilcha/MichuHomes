using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Roles
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AccessManagementService _manage;

        public CreateModel(ApplicationDbContext db, AccessManagementService manage)
        {
            _db = db;
            _manage = manage;
        }

        /// <summary>Platform staff: create a company's role (oversight). Empty: a platform role.</summary>
        [BindProperty(SupportsGet = true)] public Guid? Company { get; set; }
        [BindProperty] public RoleFormInput Input { get; set; } = new();

        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public string ScopeLabel { get; private set; } = string.Empty;
        public List<PickerItem> Picker { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadAsync();
            if (!ModelState.IsValid) return Page();
            var result = await _manage.CreateRoleAsync(Access, new RoleInput
            {
                Name = Input.Name,
                Description = Input.Description,
                IsActive = Input.IsActive,
                Permissions = Input.Permissions,
                CompanyId = Access.IsPlatform ? Company : null,
            });
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return Page();
            }
            TempData["Success"] = result.Message;
            return RedirectToPage("./Edit", new { id = result.Id });
        }

        private async Task LoadAsync()
        {
            Access = await HttpContext.AccessAsync();
            var companyId = Access.IsPlatform ? Company : Access.CompanyId;
            var scope = companyId is null ? PermissionScope.Platform : PermissionScope.Company;
            ScopeLabel = RoleForm.ScopeLabel(scope, companyId is Guid id ? await _db.HousingCompanies.Where(c => c.CompanyId == id).Select(c => c.Name).FirstOrDefaultAsync() : null);
            Picker = RoleForm.Picker(Access, scope, Input.Permissions, readOnly: false);
        }
    }
}
