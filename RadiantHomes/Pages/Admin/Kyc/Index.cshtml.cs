using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Kyc;

namespace RadiantHomes.Pages.Admin.Kyc
{
    /// <summary>Identity verification queue (kyc.view): latest application per customer, by status, oldest first.</summary>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public IndexModel(ApplicationDbContext db) => _db = db;

        [BindProperty(SupportsGet = true)] public string? Status { get; set; }
        [BindProperty(SupportsGet = true)] public string? Q { get; set; }

        public string Current => string.IsNullOrEmpty(Status) ? KycStatuses.Pending : Status;
        public Dictionary<string, int> Counts { get; private set; } = new();
        public List<Row> Rows { get; private set; } = new();

        public sealed record Row(Guid Id, string Customer, string? Email, string DocumentType, DateTime SubmittedOn, string Status, string? ReviewedBy);

        public async Task OnGetAsync()
        {
            // Only the newest application of each customer is "the" application.
            var latest = _db.KycApplications.AsNoTracking()
                .Where(a => !_db.KycApplications.Any(b => b.CustomerId == a.CustomerId && b.SubmittedOn > a.SubmittedOn));
            Counts = await latest.GroupBy(a => a.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N);

            var query = latest.Where(a => a.Status == Current);
            if (!string.IsNullOrWhiteSpace(Q))
            {
                var term = Q.Trim();
                query = query.Where(a => (a.Customer.FirstName + " " + a.Customer.LastName).Contains(term) || a.DocumentNumber.Contains(term) || (a.Customer.Email != null && a.Customer.Email.Contains(term)));
            }
            Rows = await query.OrderBy(a => a.SubmittedOn).Take(300)
                .Select(a => new Row(a.Id, a.Customer.FirstName + " " + a.Customer.LastName, a.Customer.Email, a.DocumentType, a.SubmittedOn, a.Status, a.ReviewedByName))
                .ToListAsync();
        }

        public int Count(string status) => Counts.TryGetValue(status, out var n) ? n : 0;
    }
}
