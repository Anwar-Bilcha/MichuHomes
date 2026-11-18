using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Sales;

namespace RadiantHomes.Pages.Portal.Messages
{
    /// <summary>The customer's conversations with companies (newest message first).</summary>
    public class IndexModel : PortalPageModel
    {
        public sealed record Row(Guid Id, string Subject, string Company, string Status, DateTime LastMessageOn, string? LastMessage, bool LastFromMe, int Unread);

        public List<Row> Items { get; private set; } = new();
        public bool Verified { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            var db = Service<ApplicationDbContext>();
            var me = Me.Account.Id;
            Verified = Me.Profile?.KycStatus == RadiantHomes.Models.Kyc.KycStatuses.Approved;
            Items = await db.Conversations.AsNoTracking().Where(c => c.CustomerUserId == me)
                .OrderByDescending(c => c.LastMessageOn).Take(200)
                .Select(c => new Row(c.Id, c.Subject, c.Company.Name, c.Status, c.LastMessageOn,
                    c.Messages.OrderByDescending(m => m.Id).Select(m => m.Body).FirstOrDefault(),
                    c.Messages.OrderByDescending(m => m.Id).Select(m => m.FromCustomer).FirstOrDefault(),
                    c.Messages.Count(m => !m.FromCustomer && m.ReadOn == null)))
                .ToListAsync();
            return Page();
        }
    }
}
