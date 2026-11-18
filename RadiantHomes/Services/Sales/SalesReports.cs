using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Sales;

namespace RadiantHomes.Services.Sales
{
    public sealed record CountRow(string Key, string Label, int Count);
    public sealed record AgentRow(string? UserId, string Name, int Leads, int Open, int Converted, int Lost, int VisitsDone, int Sales, double SalesValue);
    public sealed record FollowUpRow(Guid LeadId, string Reference, string Name, string Stage, DateTime Due, string? Agent);

    public sealed class LeadReport
    {
        public DateTime From { get; init; }
        public DateTime To { get; init; }
        public int NewLeads { get; init; }
        public int OpenLeads { get; init; }
        public int Converted { get; init; }
        public int Lost { get; init; }
        /// <summary>Converted ÷ (converted + lost) of the leads closed in the period; null when none closed.</summary>
        public double? ConversionRate => Converted + Lost == 0 ? null : (double)Converted / (Converted + Lost);
        public List<CountRow> ByStage { get; init; } = new();
        public List<CountRow> BySource { get; init; } = new();
        public List<CountRow> ByTemperature { get; init; } = new();
        public List<AgentRow> ByAgent { get; init; } = new();
        public List<FollowUpRow> Overdue { get; init; } = new();
        public List<FollowUpRow> Upcoming { get; init; } = new();
        public int VisitsRequested { get; init; }
        public int VisitsCompleted { get; init; }
        public int VisitsNoShow { get; init; }
        public int Sales { get; init; }
        public double SalesValue { get; init; }
        public int CallsAnswered { get; init; }
        public int CallsMissed { get; init; }
        public int Conversations { get; init; }
    }

    /// <summary>CRM figures for Staff/Leads/Report, always within the viewer's sales scope.</summary>
    public sealed class SalesReports
    {
        private readonly ApplicationDbContext _db;

        public SalesReports(ApplicationDbContext db) => _db = db;

        public async Task<LeadReport> LeadsAsync(SalesScope scope, DateTime from, DateTime to)
        {
            var fromUtc = from.Date.ToUniversalTime();
            var toUtc = to.Date.AddDays(1).ToUniversalTime();
            var leads = _db.Leads.AsNoTracking().VisibleTo(scope);
            var now = DateTime.Now;

            var newLeads = await leads.CountAsync(l => l.CreatedOn >= fromUtc && l.CreatedOn < toUtc);
            var open = await leads.CountAsync(l => l.ClosedOn == null);
            var converted = await leads.CountAsync(l => l.Stage == LeadStages.Converted && l.ClosedOn >= fromUtc && l.ClosedOn < toUtc);
            var lost = await leads.CountAsync(l => l.Stage == LeadStages.Lost && l.ClosedOn >= fromUtc && l.ClosedOn < toUtc);

            var stages = await leads.GroupBy(l => l.Stage).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N);
            var sources = await leads.Where(l => l.CreatedOn >= fromUtc && l.CreatedOn < toUtc).GroupBy(l => l.Source).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            var temps = await leads.Where(l => l.ClosedOn == null).GroupBy(l => l.Temperature).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N);

            var agentLeads = await leads.Where(l => l.CreatedOn >= fromUtc && l.CreatedOn < toUtc || l.ClosedOn == null || (l.ClosedOn >= fromUtc && l.ClosedOn < toUtc))
                .GroupBy(l => l.AgentUserId)
                .Select(g => new
                {
                    g.Key,
                    Leads = g.Count(l => l.CreatedOn >= fromUtc && l.CreatedOn < toUtc),
                    Open = g.Count(l => l.ClosedOn == null),
                    Converted = g.Count(l => l.Stage == LeadStages.Converted && l.ClosedOn >= fromUtc && l.ClosedOn < toUtc),
                    Lost = g.Count(l => l.Stage == LeadStages.Lost && l.ClosedOn >= fromUtc && l.ClosedOn < toUtc),
                }).ToListAsync();

            var visits = _db.SiteVisits.AsNoTracking().VisibleTo(scope);
            var visitsRequested = await visits.CountAsync(v => v.CreatedOn >= fromUtc && v.CreatedOn < toUtc);
            var completedVisits = await visits.Where(v => v.Status == SiteVisitStatuses.Completed && v.ScheduledStart >= from.Date && v.ScheduledStart < to.Date.AddDays(1))
                .GroupBy(v => v.AgentUserId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            var noShow = await visits.CountAsync(v => v.Status == SiteVisitStatuses.NoShow && v.ScheduledStart >= from.Date && v.ScheduledStart < to.Date.AddDays(1));

            var sales = await ScopedSales(scope).Where(t => t.Status == HouseReserveStatuses.Completed && t.SoldOn >= from.Date && t.SoldOn < to.Date.AddDays(1))
                .GroupBy(t => t.SoldByUserId).Select(g => new { g.Key, N = g.Count(), Value = g.Sum(t => t.AgreedPrice ?? 0) }).ToListAsync();

            var calls = _db.CallLogs.AsNoTracking().VisibleTo(scope).Where(c => c.RequestedOn >= fromUtc && c.RequestedOn < toUtc);
            var callsAnswered = await calls.CountAsync(c => c.AnsweredOn != null);
            var callsMissed = await calls.CountAsync(c => c.Status == CallStatuses.Missed);
            var conversations = await _db.Conversations.AsNoTracking().VisibleTo(scope).CountAsync(c => c.CreatedOn >= fromUtc && c.CreatedOn < toUtc);

            var followUps = await leads.Where(l => l.ClosedOn == null && l.NextFollowUpOn != null && l.NextFollowUpOn < now.AddDays(7))
                .OrderBy(l => l.NextFollowUpOn).Take(100)
                .Select(l => new { l.Id, l.Reference, l.Name, l.Stage, Due = l.NextFollowUpOn!.Value, l.AgentUserId }).ToListAsync();

            var userIds = agentLeads.Select(a => a.Key).Concat(completedVisits.Select(v => v.Key)).Concat(sales.Select(s => s.Key)).Concat(followUps.Select(f => f.AgentUserId))
                .Where(id => id != null).Distinct().ToList();
            var names = await NamesAsync(userIds!);

            var agents = userIds.Cast<string?>().Append(null).Distinct().Select(id =>
            {
                var l = agentLeads.FirstOrDefault(a => a.Key == id);
                var v = completedVisits.FirstOrDefault(x => x.Key == id);
                var s = sales.FirstOrDefault(x => x.Key == id);
                return new AgentRow(id, id is null ? "Not assigned" : names.GetValueOrDefault(id, "Former staff"),
                    l?.Leads ?? 0, l?.Open ?? 0, l?.Converted ?? 0, l?.Lost ?? 0, v?.N ?? 0, s?.N ?? 0, s?.Value ?? 0);
            }).Where(a => a.Leads + a.Open + a.Converted + a.Lost + a.VisitsDone + a.Sales > 0).OrderByDescending(a => a.Sales).ThenByDescending(a => a.Converted).ThenByDescending(a => a.Open).ToList();

            return new LeadReport
            {
                From = from.Date, To = to.Date,
                NewLeads = newLeads, OpenLeads = open, Converted = converted, Lost = lost,
                ByStage = LeadStages.All.Select(s => new CountRow(s, LeadStages.Label(s), stages.GetValueOrDefault(s))).ToList(),
                BySource = sources.OrderByDescending(s => s.N).Select(s => new CountRow(s.Key, LeadSources.Label(s.Key), s.N)).ToList(),
                ByTemperature = LeadTemperatures.All.Select(t => new CountRow(t, t, temps.GetValueOrDefault(t))).ToList(),
                ByAgent = agents,
                Overdue = followUps.Where(f => f.Due < now).Select(f => new FollowUpRow(f.Id, f.Reference, f.Name, f.Stage, f.Due, f.AgentUserId is string a ? names.GetValueOrDefault(a, "—") : null)).ToList(),
                Upcoming = followUps.Where(f => f.Due >= now).Select(f => new FollowUpRow(f.Id, f.Reference, f.Name, f.Stage, f.Due, f.AgentUserId is string a ? names.GetValueOrDefault(a, "—") : null)).ToList(),
                VisitsRequested = visitsRequested, VisitsCompleted = completedVisits.Sum(v => v.N), VisitsNoShow = noShow,
                Sales = sales.Sum(s => s.N), SalesValue = sales.Sum(s => s.Value),
                CallsAnswered = callsAnswered, CallsMissed = callsMissed, Conversations = conversations,
            };
        }

        /// <summary>Sales (reservation transactions) the scope may see: by the home's project, or sold by the agent.</summary>
        public IQueryable<ApartmentHouseSaleTransaction> ScopedSales(SalesScope scope)
        {
            var q = _db.ApartmentHouseSaleTransactions.AsNoTracking();
            if (!scope.IsStaff) return q.Where(_ => false);
            if (scope.IsPlatform) return q;
            var company = scope.CompanyId ?? Guid.Empty;
            q = q.Where(t => t.ApartmentHouse.Building!.Project!.HousingCompanyId == company);
            if (scope.ProjectIds is null) return q;
            var ids = scope.ProjectIds;
            var me = scope.UserId;
            return q.Where(t => ids.Contains(t.ApartmentHouse.Building!.ProjectId) || t.SoldByUserId == me);
        }

        public async Task<Dictionary<string, string>> NamesAsync(IEnumerable<string> userIds)
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return new();
            var users = await _db.AppUsers.AsNoTracking().Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName, u.Email, First = u.Customer != null ? u.Customer.FirstName : null, Last = u.Customer != null ? u.Customer.LastName : null }).ToListAsync();
            return users.ToDictionary(u => u.Id, u => !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName! : (u.First is not null ? $"{u.First} {u.Last}".Trim() : u.Email ?? "—"));
        }
    }
}
