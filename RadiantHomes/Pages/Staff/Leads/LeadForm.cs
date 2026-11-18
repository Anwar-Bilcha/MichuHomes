using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Leads
{
    /// <summary>Choices for the lead form (Create and Details): companies (platform staff), projects and agents.</summary>
    public sealed class LeadFormOptions
    {
        public List<(Guid Id, string Name)> Companies { get; init; } = new();
        public List<(Guid Id, string Name, Guid CompanyId)> Projects { get; init; } = new();
        public List<(string Id, string Name, Guid CompanyId)> Agents { get; init; } = new();
        public bool ShowCompany { get; init; }
        public bool CanAssign { get; init; }

        public static async Task<LeadFormOptions> LoadAsync(ApplicationDbContext db, SalesReports reports, UserAccess access, Guid? onlyCompany = null)
        {
            var companies = db.HousingCompanies.AsNoTracking().Where(c => c.Status == CompanyStatuses.Approved);
            if (!access.IsPlatform) companies = companies.Where(c => c.CompanyId == access.CompanyId);
            if (onlyCompany is Guid oc) companies = companies.Where(c => c.CompanyId == oc);
            var companyList = await companies.OrderBy(c => c.Name).Select(c => new { c.CompanyId, c.Name }).ToListAsync();
            var ids = companyList.Select(c => c.CompanyId).ToList();
            var projects = await db.Projects.AsNoTracking().Where(p => ids.Contains(p.HousingCompanyId)).OrderBy(p => p.ProjectName)
                .Select(p => new { p.ProjectId, p.ProjectName, p.HousingCompanyId }).ToListAsync();
            var staff = await db.AppUsers.AsNoTracking().Where(u => u.AccountType == AccountType.CompanyStaff && u.HousingCompanyId != null && ids.Contains(u.HousingCompanyId.Value))
                .Select(u => new { u.Id, Company = u.HousingCompanyId!.Value }).ToListAsync();
            var names = await reports.NamesAsync(staff.Select(s => s.Id));
            return new LeadFormOptions
            {
                Companies = companyList.Select(c => (c.CompanyId, c.Name)).ToList(),
                Projects = projects.Select(p => (p.ProjectId, p.ProjectName, p.HousingCompanyId)).ToList(),
                Agents = staff.Select(s => (s.Id, names.GetValueOrDefault(s.Id, s.Id), s.Company)).OrderBy(s => s.Item2).ToList(),
                ShowCompany = access.IsPlatform && onlyCompany is null,
                CanAssign = access.Has(Perm.LeadsAssign),
            };
        }

        public string CompanyName(Guid id) => Companies.FirstOrDefault(c => c.Id == id).Name ?? string.Empty;
    }
}
