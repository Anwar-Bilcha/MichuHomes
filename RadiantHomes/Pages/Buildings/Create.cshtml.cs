using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.Buildings
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
        [BindProperty]
        public Guid ProjectId { get; set; }
        // Problems found in an uploaded Excel file (nothing is saved when there are any)
        public List<string> UploadErrors { get; private set; } = new();

        // Every row is checked first (BuildingImport); the buildings are saved together only when the whole file is
        // valid. A Real Estate Admin's rows can only name their own company's projects.
        public async Task<IActionResult> OnPostUploadBuildingsAsync()
        {
            var scope = await CompanyScope.ForAsync(User, _context);
            if (scope.HasNoCompany) return RedirectToPage("./Index"); // the list explains why

            var (buildings, errors) = await BuildingImport.ReadAsync(file, scope.Projects(_context.Projects));
            if (errors.Count > 0)
            {
                UploadErrors = errors;
                PopulateProjects(scope);
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

        /// <param name="projectId">Preselects a project (?projectId=…).</param>
        /// <param name="id">Same, as sent by the project's "Register buildings" button (?id=…).</param>
        public async Task<IActionResult> OnGetAsync(Guid? projectId, Guid? id)
        {
            var scope = await CompanyScope.ForAsync(User, _context);
            if (scope.HasNoCompany) return RedirectToPage("./Index"); // the list explains why
            Building = new Building { ProjectId = projectId ?? id ?? Guid.Empty };
            PopulateProjects(scope);
            return Page();
        }

        [BindProperty]
        public Building Building { get; set; } = new Building();

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            var scope = await CompanyScope.ForAsync(User, _context);
            if (scope.HasNoCompany) return RedirectToPage("./Index");

            // The form posts Building.ProjectId; the top-level ProjectId is only set by other callers.
            if (ProjectId != Guid.Empty)
                Building.ProjectId = ProjectId;

            BuildingOptions.Validate(ModelState, Building);
            // An empty project choice is a binding error ("The value '' is invalid."); replaced by the check below.
            ModelState.Remove($"{nameof(Building)}.{nameof(Building.ProjectId)}");
            // Only the user's own projects (all projects for a Michu Admin)
            var project = await scope.Projects(_context.Projects).AsNoTracking().FirstOrDefaultAsync(a => a.ProjectId == Building.ProjectId);
            if (project == null)
                ModelState.AddModelError($"{nameof(Building)}.{nameof(Building.ProjectId)}", "Please select the project this building belongs to.");

            if (!ModelState.IsValid)
            {
                PopulateProjects(scope);
                return Page();
            }

            // The building takes its project's address; CreatedOn/CreatedBy are stamped by ApplicationDbContext.
            Building.AddressId = project!.AddressId;
            Building.Id = Guid.NewGuid();
            _context.Buildings.Add(Building);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

        private void PopulateProjects(CompanyScope scope) => ViewData["ProjectId"] = ProjectSelectList(_context, scope, Building?.ProjectId);

        /// <summary>Projects by name (with their company), for the Create and Edit dropdowns: a Real Estate Admin's own only.</summary>
        internal static SelectList ProjectSelectList(ApplicationDbContext context, CompanyScope scope, Guid? selected)
        {
            var projects = scope.Projects(context.Projects)
                .AsNoTracking()
                .Include(p => p.Company)
                .OrderBy(p => p.ProjectName)
                .Select(p => new { p.ProjectId, Name = p.Company != null ? p.ProjectName + " (" + p.Company.Name + ")" : p.ProjectName })
                .ToList();
            return new SelectList(projects, "ProjectId", "Name", selected);
        }


    }
}
