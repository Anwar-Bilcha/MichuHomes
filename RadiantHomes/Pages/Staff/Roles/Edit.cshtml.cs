using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Roles
{
    /// <summary>One role: view its permissions and holders; edit, deactivate or delete it (not built-in roles).</summary>
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AccessManagementService _manage;

        public EditModel(ApplicationDbContext db, AccessManagementService manage)
        {
            _db = db;
            _manage = manage;
        }

        [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
        [BindProperty] public RoleFormInput Input { get; set; } = new();

        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public AccessRole Role { get; private set; } = null!;
        public string ScopeLabel { get; private set; } = string.Empty;
        public bool ReadOnly { get; private set; }
        public List<PickerItem> Picker { get; private set; } = new();
        public List<(string Id, string Name, string Email, bool Active)> Holders { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await LoadAsync()) return NotFound();
            Input = new RoleFormInput
            {
                Name = Role.Name,
                Description = Role.Description,
                IsActive = Role.IsActive,
                Permissions = Role.Permissions.Select(p => p.PermissionKey).ToList(),
            };
            Picker = RoleForm.Picker(Access, Role.Scope, Input.Permissions, ReadOnly);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!await LoadAsync()) return NotFound();
            if (!ModelState.IsValid)
            {
                Picker = RoleForm.Picker(Access, Role.Scope, Input.Permissions, ReadOnly);
                return Page();
            }
            var result = await _manage.UpdateRoleAsync(Access, Id, new RoleInput
            {
                Name = Input.Name,
                Description = Input.Description,
                IsActive = Input.IsActive,
                Permissions = Input.Permissions,
            });
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                Picker = RoleForm.Picker(Access, Role.Scope, Input.Permissions, ReadOnly);
                return Page();
            }
            TempData["Success"] = result.Message;
            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostDeleteAsync()
        {
            if (!await LoadAsync()) return NotFound();
            var companyId = Role.CompanyId;
            var result = await _manage.DeleteRoleAsync(Access, Id);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return result.Succeeded ? RedirectToPage("./Index", new { company = Access.IsPlatform ? companyId : null }) : RedirectToPage(new { id = Id });
        }

        private async Task<bool> LoadAsync()
        {
            Access = await HttpContext.AccessAsync();
            var role = await _manage.VisibleRoles(Access).AsNoTracking().Include(r => r.Permissions).Include(r => r.Company).FirstOrDefaultAsync(r => r.Id == Id);
            if (role is null) return false;
            Role = role;
            ReadOnly = role.IsSystem || !Access.Has(Perm.RolesManage);
            ScopeLabel = RoleForm.ScopeLabel(role.Scope, role.Company?.Name);
            var holders = await _db.AppUsers.AsNoTracking().Include(u => u.Customer)
                .Where(u => _db.UserAccessRoles.Any(ur => ur.RoleId == Id && ur.UserId == u.Id)).ToListAsync();
            Holders = holders.Select(u => (u.Id, AccessManagementService.DisplayName(u), u.Email ?? string.Empty, AccessManagementService.IsActive(u)))
                .OrderBy(h => h.Item2).ToList();
            return true;
        }
    }
}
