using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Audit
{
    /// <summary>Audit log: platform staff see every entry; company staff see their company's entries only.</summary>
    public class IndexModel : PageModel
    {
        private const int PageSize = 50;
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db) => _db = db;

        [BindProperty(SupportsGet = true)] public string? Category { get; set; }
        [BindProperty(SupportsGet = true)] public string? Q { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
        [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1;

        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public List<AuditLog> Entries { get; private set; } = new();
        public Dictionary<Guid, string> Companies { get; private set; } = new();
        public int Total { get; private set; }
        public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
        public static readonly string[] Categories = { AuditCategories.Users, AuditCategories.Roles, AuditCategories.Companies, AuditCategories.Security };

        public async Task OnGetAsync()
        {
            Access = await HttpContext.AccessAsync();
            var query = _db.AuditLogs.AsNoTracking();
            if (!Access.IsPlatform)
            {
                var own = Access.CompanyId ?? Guid.Empty;
                query = query.Where(a => a.CompanyId == own);
            }
            if (!string.IsNullOrWhiteSpace(Category)) query = query.Where(a => a.Category == Category);
            if (!string.IsNullOrWhiteSpace(Q))
            {
                var term = Q.Trim();
                query = query.Where(a => a.Summary.Contains(term) || (a.ActorName != null && a.ActorName.Contains(term)) || a.Action.Contains(term));
            }
            if (From is DateTime from) query = query.Where(a => a.OccurredOn >= from.Date.ToUniversalTime());
            if (To is DateTime to) query = query.Where(a => a.OccurredOn < to.Date.AddDays(1).ToUniversalTime());

            Total = await query.CountAsync();
            PageNumber = Math.Clamp(PageNumber, 1, Pages);
            Entries = await query.OrderByDescending(a => a.OccurredOn).ThenByDescending(a => a.Id)
                .Skip((PageNumber - 1) * PageSize).Take(PageSize).ToListAsync();

            if (Access.IsPlatform)
            {
                var ids = Entries.Where(e => e.CompanyId != null).Select(e => e.CompanyId!.Value).Distinct().ToList();
                Companies = await _db.HousingCompanies.AsNoTracking().Where(c => ids.Contains(c.CompanyId)).ToDictionaryAsync(c => c.CompanyId, c => c.Name);
            }
        }
    }
}
