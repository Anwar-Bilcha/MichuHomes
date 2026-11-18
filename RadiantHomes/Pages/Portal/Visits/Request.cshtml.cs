using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Sales;

namespace RadiantHomes.Pages.Portal.Visits
{
    /// <summary>Ask to visit a home (?apartment=) or project (?project=) on a day and time; the company confirms it.</summary>
    public class RequestModel : PortalPageModel
    {
        [BindProperty(SupportsGet = true)] public Guid? Apartment { get; set; }
        [BindProperty(SupportsGet = true)] public Guid? Project { get; set; }

        [BindProperty, Required(ErrorMessage = "Choose a day."), DataType(DataType.Date)]
        [Display(Name = "Day")]
        public DateTime? Date { get; set; }

        [BindProperty, Required(ErrorMessage = "Choose a time.")]
        [Display(Name = "Time")]
        public string? Time { get; set; }

        [BindProperty, StringLength(500)]
        [Display(Name = "Anything the company should know? (optional)")]
        public string? Notes { get; set; }

        public string? About { get; private set; }
        public string? Company { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (!Me.HasProfile) return CompleteProfileFirst();
            if (!await LoadAboutAsync()) return NotFound();
            Date = DateTime.Today.AddDays(1);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (!Me.HasProfile) return CompleteProfileFirst();
            if (!await LoadAboutAsync()) return NotFound();
            if (!ModelState.IsValid) return Page();
            var result = await Service<SiteVisitService>().RequestAsync(Me.Account.Id, new VisitRequest(Apartment is null ? Project : null, Apartment, Date!.Value, Time!, Notes));
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return Page();
            }
            TempData["Success"] = result.Message;
            return RedirectToPage("./Index");
        }

        private async Task<bool> LoadAboutAsync()
        {
            var db = Service<ApplicationDbContext>();
            if (Apartment is Guid aid)
            {
                var home = await db.ApartmentHouses.AsNoTracking().Where(a => a.Id == aid && a.Building!.Project!.Company!.Status == CompanyStatuses.Approved)
                    .Select(a => new { a.Title, Project = a.Building!.Project!.ProjectName, Company = a.Building.Project.Company!.Name }).FirstOrDefaultAsync();
                if (home is null) return false;
                About = $"{home.Title} · {home.Project}"; Company = home.Company;
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
    }
}
