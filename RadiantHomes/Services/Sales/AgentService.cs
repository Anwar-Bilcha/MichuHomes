using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Sales;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Services.Sales
{
    /// <summary>
    /// Which sales items (chats, site visits, leads, reservations) a staff member sees. Platform staff: everything.
    /// Company staff with sales.all_projects: their whole company. Other company staff (agents): only the projects they
    /// are assigned to, plus anything assigned to them personally.
    /// </summary>
    public sealed class SalesScope
    {
        public static readonly SalesScope Nobody = new(false, null, new List<Guid>(), null, false);

        public SalesScope(bool isPlatform, Guid? companyId, List<Guid>? projectIds, string? userId, bool isStaff)
        {
            IsPlatform = isPlatform;
            CompanyId = companyId;
            ProjectIds = projectIds;
            UserId = userId;
            IsStaff = isStaff;
        }

        public bool IsPlatform { get; }
        public Guid? CompanyId { get; }
        /// <summary>Null: every project of the company (or of all companies for platform staff).</summary>
        public List<Guid>? ProjectIds { get; }
        public string? UserId { get; }
        public bool IsStaff { get; }
        public bool SeesWholeCompany => IsPlatform || ProjectIds is null;

        public bool CanSee(Guid companyId, Guid? projectId, string? assignedUserId) =>
            IsStaff && (IsPlatform || (CompanyId == companyId && (ProjectIds is null || (projectId is Guid p && ProjectIds.Contains(p)) || (assignedUserId is not null && assignedUserId == UserId))));
    }

    public static class SalesScopeQueries
    {
        public static IQueryable<Conversation> VisibleTo(this IQueryable<Conversation> q, SalesScope s)
        {
            if (!s.IsStaff) return q.Where(_ => false);
            if (s.IsPlatform) return q;
            var company = s.CompanyId ?? Guid.Empty;
            if (s.ProjectIds is null) return q.Where(c => c.CompanyId == company);
            var ids = s.ProjectIds;
            var me = s.UserId;
            return q.Where(c => c.CompanyId == company && ((c.ProjectId != null && ids.Contains(c.ProjectId.Value)) || c.AgentUserId == me));
        }

        public static IQueryable<SiteVisit> VisibleTo(this IQueryable<SiteVisit> q, SalesScope s)
        {
            if (!s.IsStaff) return q.Where(_ => false);
            if (s.IsPlatform) return q;
            var company = s.CompanyId ?? Guid.Empty;
            if (s.ProjectIds is null) return q.Where(v => v.CompanyId == company);
            var ids = s.ProjectIds;
            var me = s.UserId;
            return q.Where(v => v.CompanyId == company && (ids.Contains(v.ProjectId) || v.AgentUserId == me));
        }

        public static IQueryable<Lead> VisibleTo(this IQueryable<Lead> q, SalesScope s)
        {
            if (!s.IsStaff) return q.Where(_ => false);
            if (s.IsPlatform) return q;
            var company = s.CompanyId ?? Guid.Empty;
            if (s.ProjectIds is null) return q.Where(l => l.CompanyId == company);
            var ids = s.ProjectIds;
            var me = s.UserId;
            return q.Where(l => l.CompanyId == company && ((l.ProjectId != null && ids.Contains(l.ProjectId.Value)) || l.AgentUserId == me));
        }

        public static IQueryable<CallLog> VisibleTo(this IQueryable<CallLog> q, SalesScope s)
        {
            if (!s.IsStaff) return q.Where(_ => false);
            if (s.IsPlatform) return q;
            var company = s.CompanyId ?? Guid.Empty;
            if (s.ProjectIds is null) return q.Where(c => c.CompanyId == company);
            var ids = s.ProjectIds;
            var me = s.UserId;
            return q.Where(c => c.CompanyId == company && ((c.ProjectId != null && ids.Contains(c.ProjectId.Value)) || c.AgentUserId == me));
        }
    }

    /// <summary>Sales agents on projects: assignment (with history), routing, and each user's sales scope.</summary>
    public sealed class AgentService
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public AgentService(ApplicationDbContext db, AuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public async Task<SalesScope> ScopeAsync(UserAccess access)
        {
            if (!access.IsStaff) return SalesScope.Nobody;
            if (access.IsPlatform) return new SalesScope(true, null, null, access.UserId, true);
            if (access.Has(Perm.AllProjects)) return new SalesScope(false, access.CompanyId, null, access.UserId, true);
            var projects = await _db.ProjectAgentAssignments.AsNoTracking()
                .Where(a => a.UserId == access.UserId && a.EndedOn == null).Select(a => a.ProjectId).ToListAsync();
            return new SalesScope(false, access.CompanyId, projects, access.UserId, true);
        }

        /// <summary>The project's agent with the least open work, or null when nobody is assigned.</summary>
        public async Task<string?> PickAgentAsync(Guid? projectId)
        {
            if (projectId is not Guid id) return null;
            var agents = await _db.ProjectAgentAssignments.AsNoTracking()
                .Where(a => a.ProjectId == id && a.EndedOn == null
                            && !(a.User.LockoutEnabled && a.User.LockoutEnd != null && a.User.LockoutEnd > DateTimeOffset.UtcNow))
                .Select(a => a.UserId).ToListAsync();
            if (agents.Count == 0) return null;
            var load = await _db.Conversations.Where(c => c.Status == ConversationStatuses.Open && c.AgentUserId != null && agents.Contains(c.AgentUserId))
                .GroupBy(c => c.AgentUserId!).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.N);
            return agents.OrderBy(a => load.TryGetValue(a, out var n) ? n : 0).ThenBy(a => a).First();
        }

        public async Task<AccessResult> AssignAsync(UserAccess actor, Guid projectId, string userId, string? actorName)
        {
            var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectId == projectId);
            if (project is null || !actor.Can(Perm.AgentsAssign, project.HousingCompanyId)) return AccessResult.Fail("You can't assign agents to that project.");
            var user = await _db.AppUsers.Include(u => u.Customer).FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null || user.AccountType != AccountType.CompanyStaff || user.HousingCompanyId != project.HousingCompanyId)
                return AccessResult.Fail("Only the company's own staff can be its agents.");
            if (await _db.ProjectAgentAssignments.AnyAsync(a => a.ProjectId == projectId && a.UserId == userId && a.EndedOn == null))
                return AccessResult.Fail($"{AccessManagementService.DisplayName(user)} is already an agent of {project.ProjectName}.");
            _db.ProjectAgentAssignments.Add(new ProjectAgentAssignment { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId, AssignedOn = DateTime.UtcNow, AssignedBy = actorName });
            _audit.Add(AuditCategories.Sales, "AgentAssigned", $"{AccessManagementService.DisplayName(user)} assigned to {project.ProjectName}.", project.HousingCompanyId, "HouseProject", projectId.ToString());
            await _db.SaveChangesAsync();
            return AccessResult.Ok($"{AccessManagementService.DisplayName(user)} is now an agent of {project.ProjectName}.");
        }

        public async Task<AccessResult> EndAsync(UserAccess actor, Guid assignmentId, string? actorName)
        {
            var a = await _db.ProjectAgentAssignments.Include(x => x.Project).Include(x => x.User).ThenInclude(u => u.Customer).FirstOrDefaultAsync(x => x.Id == assignmentId);
            if (a is null || a.EndedOn is not null || !actor.Can(Perm.AgentsAssign, a.Project.HousingCompanyId)) return AccessResult.Fail("That assignment can't be ended.");
            a.EndedOn = DateTime.UtcNow;
            a.EndedBy = actorName;
            _audit.Add(AuditCategories.Sales, "AgentUnassigned", $"{AccessManagementService.DisplayName(a.User)} is no longer an agent of {a.Project.ProjectName}.", a.Project.HousingCompanyId, "HouseProject", a.ProjectId.ToString());
            await _db.SaveChangesAsync();
            return AccessResult.Ok($"{AccessManagementService.DisplayName(a.User)} was removed from {a.Project.ProjectName}.");
        }

        public static string NewReference(string prefix)
        {
            const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
            Span<char> code = stackalloc char[5];
            for (var i = 0; i < code.Length; i++) code[i] = alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length)];
            return $"{prefix}-{DateTime.UtcNow:yyMM}-{code}";
        }
    }
}
