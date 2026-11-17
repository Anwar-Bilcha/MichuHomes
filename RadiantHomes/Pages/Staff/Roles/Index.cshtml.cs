using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Roles
{
    /// <summary>Roles of the user's domain (platform staff: platform roles, and any company's for oversight).</summary>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AccessManagementService _manage;

        public IndexModel(ApplicationDbContext db, AccessManagementService manage)
        {
            _db = db;
            _manage = manage;
        }

        /// <summary>Platform staff: "" (platform roles) or a company id.</summary>
        [BindProperty(SupportsGet = true)] public Guid? Company { get; set; }

        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public List<SelectListItem> CompanyOptions { get; private set; } = new();
        public string ScopeLabel { get; private set; } = string.Empty;
        public List<Row> Rows { get; private set; } = new();

        public sealed record Row(Guid Id, string Name, string? Description, bool IsSystem, bool IsActive, int Permissions, int Holders);

        public async Task OnGetAsync()
        {
            Access = await HttpContext.AccessAsync();
            var query = _manage.VisibleRoles(Access).AsNoTracking();
            if (Access.IsPlatform)
            {
                query = Company is Guid c ? query.Where(r => r.CompanyId == c) : query.Where(r => r.Scope == PermissionScope.Platform);
                var companies = await _db.HousingCompanies.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.CompanyId, x.Name }).ToListAsync();
                CompanyOptions = new List<SelectListItem> { new("Platform roles (Michu Homes)", "") };
                CompanyOptions.AddRange(companies.Select(x => new SelectListItem(x.Name, x.CompanyId.ToString())));
            }
            var companyId = Access.IsPlatform ? Company : Access.CompanyId;
            ScopeLabel = RoleForm.ScopeLabel(companyId is null ? PermissionScope.Platform : PermissionScope.Company,
                companyId is Guid id ? await _db.HousingCompanies.Where(x => x.CompanyId == id).Select(x => x.Name).FirstOrDefaultAsync() : null);

            Rows = await query
                .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
                .Select(r => new Row(r.Id, r.Name, r.Description, r.IsSystem, r.IsActive, r.Permissions.Count, r.Users.Count))
                .ToListAsync();
        }
    }
}
