using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Security;

namespace RadiantHomes.Pages.Admin.Companies
{
    /// <summary>Company approvals (platform): companies by approval status, waiting ones first.</summary>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db) => _db = db;

        /// <summary>A CompanyStatuses value; default Pending. "all" shows everything.</summary>
        [BindProperty(SupportsGet = true)] public string? Status { get; set; }
        [BindProperty(SupportsGet = true)] public string? Q { get; set; }

        public Dictionary<string, int> Counts { get; private set; } = new();
        public List<Row> Rows { get; private set; } = new();
        public string Current => string.IsNullOrEmpty(Status) ? CompanyStatuses.Pending : Status;

        public sealed record Row(Guid Id, string Name, string License, string Email, string Phone, string Status, DateTime? Since, int Staff, int Listed);

        public async Task OnGetAsync()
        {
            Counts = await _db.HousingCompanies.GroupBy(c => c.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N);
            var query = _db.HousingCompanies.AsNoTracking();
            if (Current != "all") query = query.Where(c => c.Status == Current);
            if (!string.IsNullOrWhiteSpace(Q))
            {
                var term = Q.Trim();
                query = query.Where(c => c.Name.Contains(term) || c.LicenseNumber.Contains(term) || c.Email.Contains(term));
            }
            Rows = await query
                .OrderBy(c => c.Name)
                .Select(c => new Row(
                    c.CompanyId, c.Name, c.LicenseNumber, c.Email, c.PhoneNumber, c.Status,
                    _db.CompanyStatusChanges.Where(h => h.CompanyId == c.CompanyId).Max(h => (DateTime?)h.ChangedOn),
                    _db.AppUsers.Count(u => u.HousingCompanyId == c.CompanyId && u.AccountType == AccountType.CompanyStaff),
                    _db.ApartmentHouses.Count(a => a.Building!.Project!.HousingCompanyId == c.CompanyId && a.IsAvailable && a.Status == "Approved")))
                .ToListAsync();
        }

        public int Count(string status) => Counts.TryGetValue(status, out var n) ? n : 0;
    }
}
