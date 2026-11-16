using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Pages.Buildings;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Utilities;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.Buildings
{
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        [BindProperty]
        public IFormFile? file { get; set; }
        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }
        [BindProperty(SupportsGet = true)]
        public Guid ProjectId { get; set; }

        // Problems found in an uploaded Excel file (nothing is saved when there are any)
        public List<string> UploadErrors { get; private set; } = new();

        // Every row is checked first; the buildings are saved together only when the whole file is valid.
        // A Real Estate Admin's rows can only name their own company's projects.
        public async Task<IActionResult> OnPostUploadBuildingsAsync()
        {
            var scope = await CompanyScope.ForAsync(User, _context);
            if (scope.HasNoCompany) return RedirectToPage("./Index"); // the list explains why

            var (buildings, errors) = await BuildingImport.ReadAsync(file, scope.Projects(_context.Projects));
            if (errors.Count > 0)
            {
                UploadErrors = errors;
                await LoadSelectListsAsync(scope);
                return Page();
            }

            // All or nothing: one SaveChanges (a single transaction) for the whole file
            _context.Buildings.AddRange(buildings);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{buildings.Count} building{(buildings.Count == 1 ? "" : "s")} uploaded.";
            return RedirectToPage("./Index");
        }

        public IActionResult OnGetDownloadTemplate()
        {
            var filePath = Path.Combine(_env.WebRootPath, "templates", "BuildingTemplate.xlsx");
            var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return PhysicalFile(filePath, contentType, "BuildingTemplate.xlsx");
        }

        public async Task<IActionResult> OnGetAsync(Guid projectId)
        {
            var scope = await CompanyScope.ForAsync(User, _context);
            if (scope.HasNoCompany) return RedirectToPage("./Index");
            if (projectId != Guid.Empty) Building.ProjectId = projectId; // preselect the project from ?projectId=
            await LoadSelectListsAsync(scope);
            return Page();
        }

        [BindProperty]
        public Building Building { get; set; } = new Building();

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            var scope = await CompanyScope.ForAsync(User, _context);
            if (scope.HasNoCompany) return RedirectToPage("./Index");

            // The form posts the project from its dropdown (Building.ProjectId); ?projectId= is only a fallback.
            if (Building.ProjectId == Guid.Empty) Building.ProjectId = ProjectId;

            // Navigation properties aren't posted; the address isn't on this form either (see below)
            ModelState.Remove("Building.Address");
            ModelState.Remove("Building.Project");

            // Only the user's own projects (all projects for a Michu Admin)
            var project = Building.ProjectId == Guid.Empty
                ? null
                : await scope.Projects(_context.Projects).FirstOrDefaultAsync(p => p.ProjectId == Building.ProjectId);
            if (project == null)
            {
                ModelState.AddModelError("Building.ProjectId", "Please select a project.");
            }

            // Type and construction status are stored as the enum names (the Excel upload accepts the same values)
            if (BuildingOptions.TryParseType(Building.BuildingType, out var buildingType))
                Building.BuildingType = buildingType.ToString();
            else
                ModelState.AddModelError("Building.BuildingType", $"Choose one of: {AllowedValues<BuildingTypes>()}.");
            if (BuildingOptions.TryParseStatus(Building.ConstructionStatus, out var constructionStatus))
                Building.ConstructionStatus = constructionStatus.ToString();
            else
                ModelState.AddModelError("Building.ConstructionStatus", $"Choose one of: {AllowedValues<ConstructionStatuses>()}.");

            if (!ModelState.IsValid)
            {
                await LoadSelectListsAsync(scope);
                return Page();
            }

            // Like the Excel upload, a new building gets its project's address (it can be changed on Edit).
            // Id and CreatedOn/CreatedBy are filled in by ApplicationDbContext.SaveChanges.
            Building.AddressId = project!.AddressId;
            _context.Buildings.Add(Building);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        private async Task LoadSelectListsAsync(CompanyScope scope)
        {
            var projects = await scope.Projects(_context.Projects).OrderBy(p => p.ProjectName)
                .Select(p => new { p.ProjectId, p.ProjectName }).ToListAsync();
            ViewData["ProjectId"] = new SelectList(projects, "ProjectId", "ProjectName", Building.ProjectId);
        }

        private static string AllowedValues<TEnum>() where TEnum : struct, Enum =>
            string.Join(", ", Enum.GetValues<TEnum>().Select(v => v.GetDisplayName()));
    }
}
