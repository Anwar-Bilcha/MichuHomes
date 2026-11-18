using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Sales;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;
using RadiantHomes.Services.VideoCalls;

namespace RadiantHomes.Pages.Staff.Calls
{
    /// <summary>Video calls: customers waiting now (join or decline with a reason) and the call history.</summary>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AgentService _agents;
        private readonly VideoCallService _calls;

        public IndexModel(ApplicationDbContext db, AgentService agents, VideoCallService calls)
        {
            _db = db;
            _agents = agents;
            _calls = calls;
        }

        [BindProperty(SupportsGet = true)] public string? Status { get; set; }
        [BindProperty] public string? Reason { get; set; }

        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public List<CallLog> Waiting { get; private set; } = new();
        public List<CallLog> Items { get; private set; } = new();
        public Dictionary<string, int> Counts { get; private set; } = new();
        public bool CanAnswer => Access.Has(Perm.VideoCallsReceive);
        public bool CanSeeHistory => Access.Has(Perm.CallHistoryView);

        public async Task OnGetAsync()
        {
            Access = await HttpContext.AccessAsync();
            var scope = await _agents.ScopeAsync(Access);
            await _calls.ExpireMissedAsync();
            var all = _db.CallLogs.AsNoTracking().VisibleTo(scope);
            if (!scope.IsPlatform) all = all.Where(c => c.CompanyId != null);
            if (CanAnswer) Waiting = await all.Where(c => c.Status == CallStatuses.Waiting).OrderBy(c => c.RequestedOn).Take(50).ToListAsync();
            if (!CanSeeHistory) return;
            var since = DateTime.UtcNow.AddDays(-90);
            Counts = await all.Where(c => c.RequestedOn >= since).GroupBy(c => c.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N);
            var q = all.Where(c => c.Status != CallStatuses.Waiting);
            if (!string.IsNullOrEmpty(Status)) q = q.Where(c => c.Status == Status);
            Items = await q.OrderByDescending(c => c.RequestedOn).Take(300).ToListAsync();
        }

        public async Task<IActionResult> OnPostRejectAsync(Guid id)
        {
            var access = await HttpContext.AccessAsync();
            var result = await _calls.RejectAsync(access, await _agents.ScopeAsync(access), id, Reason, User.Identity?.Name);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToPage(new { status = Status });
        }

        public int Count(string status) => Counts.TryGetValue(status, out var n) ? n : 0;

        public static string Duration(CallLog c) => c.AnsweredOn is DateTime a && c.EndedOn is DateTime e
            ? (e - a).TotalMinutes >= 1 ? $"{(int)(e - a).TotalMinutes} min {(e - a).Seconds} s" : $"{(int)(e - a).TotalSeconds} s"
            : "—";
    }
}
