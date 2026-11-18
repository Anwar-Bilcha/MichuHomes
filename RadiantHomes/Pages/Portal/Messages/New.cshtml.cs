using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Kyc;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Sales;

namespace RadiantHomes.Pages.Portal.Messages
{
    /// <summary>
    /// Start a conversation with the company of a home (?apartment=) or project (?project=). Only customers with a
    /// verified identity can write; an existing conversation about the same home opens instead.
    /// </summary>
    public class NewModel : PortalPageModel
    {
        [BindProperty(SupportsGet = true)] public Guid? Apartment { get; set; }
        [BindProperty(SupportsGet = true)] public Guid? Project { get; set; }
        [BindProperty] public string? Body { get; set; }
        [BindProperty] public Guid ClientMessageId { get; set; }

        public string? About { get; private set; }
        public string? Company { get; private set; }
        public bool Verified { get; private set; }
        public string? Error { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (!Me.HasProfile) return CompleteProfileFirst();
            if (!await LoadAboutAsync()) return NotFound();
            var existing = await ExistingAsync();
            if (existing is Guid id) return RedirectToPage("./Conversation", new { id });
            ClientMessageId = Guid.NewGuid();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (!Me.HasProfile) return CompleteProfileFirst();
            if (!await LoadAboutAsync()) return NotFound();
            var result = await Service<ChatService>().StartAsync(Me.Account.Id, Apartment, Apartment is null ? Project : null, Body ?? string.Empty, ClientMessageId);
            if (!result.Succeeded)
            {
                Error = result.Message;
                return Page();
            }
            return RedirectToPage("./Conversation", new { id = result.ConversationId });
        }

        private async Task<bool> LoadAboutAsync()
        {
            var db = Service<ApplicationDbContext>();
            Verified = Me.Profile?.KycStatus == KycStatuses.Approved;
            if (Apartment is Guid aid)
            {
                var home = await db.ApartmentHouses.AsNoTracking().Where(a => a.Id == aid && a.Building!.Project!.Company!.Status == CompanyStatuses.Approved)
                    .Select(a => new { a.Title, Company = a.Building!.Project!.Company!.Name }).FirstOrDefaultAsync();
                if (home is null) return false;
                About = home.Title; Company = home.Company;
                return true;
            }
            if (Project is Guid pid)
            {
                var project = await db.Projects.AsNoTracking().Where(p => p.ProjectId == pid && p.Company!.Status == CompanyStatuses.Approved)
                    .Select(p => new { p.ProjectName, Company = p.Company!.Name }).FirstOrDefaultAsync();
                if (project is null) return false;
                About = project.ProjectName; Company = project.Company;
                return true;
            }
            return false;
        }

        private async Task<Guid?> ExistingAsync()
        {
            var db = Service<ApplicationDbContext>();
            var me = Me.Account.Id;
            var q = db.Conversations.AsNoTracking().Where(c => c.CustomerUserId == me);
            q = Apartment is Guid aid ? q.Where(c => c.ApartmentHouseId == aid) : q.Where(c => c.ApartmentHouseId == null && c.ProjectId == Project);
            return await q.Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
        }
    }
}
