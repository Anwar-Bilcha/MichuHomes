using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Users
{
    /// <summary>
    /// Staff accounts. Company staff see their own company's; platform staff see platform staff and (oversight) every
    /// company's, filtered by company. Customers are not listed here.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AccessManagementService _manage;

        public IndexModel(ApplicationDbContext db, AccessManagementService manage)
        {
            _db = db;
            _manage = manage;
        }

        [BindProperty(SupportsGet = true)] public string? Q { get; set; }
        /// <summary>Platform staff only: "platform", or a company id.</summary>
        [BindProperty(SupportsGet = true)] public string? Scope { get; set; }

        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public List<Row> Rows { get; private set; } = new();
        public List<SelectListItem> ScopeOptions { get; private set; } = new();
        public string? CompanyName { get; private set; }

        public sealed record Row(string Id, string Name, string Email, string? Company, bool Active, AccountType Type, List<string> Roles, bool IsYou);

        public async Task OnGetAsync()
        {
            Access = await HttpContext.AccessAsync();
            var query = _manage.VisibleStaff(Access).AsNoTracking();

            if (Access.IsPlatform)
            {
                if (Scope == "platform") query = query.Where(u => u.AccountType == AccountType.PlatformStaff);
                else if (Guid.TryParse(Scope, out var companyId)) query = query.Where(u => u.HousingCompanyId == companyId);
                var companies = await _db.HousingCompanies.AsNoTracking().OrderBy(c => c.Name).Select(c => new { c.CompanyId, c.Name }).ToListAsync();
                ScopeOptions = new List<SelectListItem> { new("Everyone", ""), new("Platform staff", "platform") };
                ScopeOptions.AddRange(companies.Select(c => new SelectListItem(c.Name, c.CompanyId.ToString())));
            }
            else if (Access.CompanyId is Guid own)
            {
                CompanyName = await _db.HousingCompanies.Where(c => c.CompanyId == own).Select(c => c.Name).FirstOrDefaultAsync();
            }

            if (!string.IsNullOrWhiteSpace(Q))
            {
                var term = Q.Trim();
                query = query.Where(u => (u.Email != null && u.Email.Contains(term)) || (u.FullName != null && u.FullName.Contains(term))
                                         || (u.Customer != null && (u.Customer.FirstName + " " + u.Customer.LastName).Contains(term)));
            }

            var users = await query.Include(u => u.Company).Include(u => u.Customer)
                .OrderBy(u => u.AccountType).ThenBy(u => u.FullName ?? u.Email)
                .Take(500).ToListAsync();
            var ids = users.Select(u => u.Id).ToList();
            var roles = await _db.UserAccessRoles.AsNoTracking().Where(ur => ids.Contains(ur.UserId))
                .Select(ur => new { ur.UserId, ur.Role.Name, ur.Role.IsActive }).ToListAsync();

            Rows = users.Select(u => new Row(
                u.Id,
                AccessManagementService.DisplayName(u),
                u.Email ?? u.UserName ?? string.Empty,
                u.AccountType == AccountType.PlatformStaff ? null : u.Company?.Name,
                AccessManagementService.IsActive(u),
                u.AccountType,
                roles.Where(r => r.UserId == u.Id).Select(r => r.IsActive ? r.Name : r.Name + " (inactive role)").OrderBy(n => n).ToList(),
                u.Id == Access.UserId)).ToList();
        }
    }
}
