using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting; // For IWebHostEnvironment
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // For [Required], [Range], [Display]
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies; // Assuming HouseProject is here
using RadiantHomes.Models; // Assuming ApartmentHouse (if used directly, otherwise just for context)
using RadiantHomes.Utilities;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.ApartmentHouses
{
    public class UploadPhotosModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public UploadPhotosModel(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // Problems with the submitted form: shown inline above the forms when the page is re-displayed.
        // (Success redirects back with TempData["Success"], shown as a toast by _FlashMessages.)
        public string? ErrorMessage { get; set; }

        // --- Apartment House Photo Upload Properties ---
        [BindProperty]
        public ApartmentUploadInputModel ApartmentUpload { get; set; } = new ApartmentUploadInputModel();

        // --- House Project Photo Upload Properties ---
        [BindProperty]
        public ProjectUploadInputModel ProjectUpload { get; set; } = new ProjectUploadInputModel();

        // SelectList for House Projects dropdown
        public SelectList? ProjectsSelectList { get; set; }

        // A Real Estate Admin without a company: nothing to upload for
        public bool NoCompany { get; set; }

        // A Real Estate Admin's apartment photos are always filed under their own company (the name can't be changed)
        public string? FixedCompanyName { get; set; }

        // Set by each handler: the user's company scope
        private CompanyScope _scope = default!;

        private async Task LoadScopeAsync()
        {
            _scope = await CompanyScope.ForAsync(User, _context);
            NoCompany = _scope.HasNoCompany;
            if (_scope.IsRestricted && _scope.CompanyId is Guid ownCompanyId)
            {
                FixedCompanyName = await _context.HousingCompanies.Where(c => c.CompanyId == ownCompanyId).Select(c => c.Name).FirstOrDefaultAsync();
            }
        }

        public async Task OnGetAsync()
        {
            await LoadScopeAsync();
            // Initialize input models to ensure they are not null when the page loads
            ApartmentUpload = new ApartmentUploadInputModel { CompanyName = FixedCompanyName ?? string.Empty };
            ProjectUpload = new ProjectUploadInputModel();

            // Load House Projects for the dropdown
            await LoadProjectsSelectListAsync();
        }

        // Handler for Apartment House Photo Upload Form
        public async Task<IActionResult> OnPostApartmentPhotosAsync()
        {
            await LoadScopeAsync();
            if (NoCompany) return Page();
            if (_scope.IsRestricted)
            {
                // The posted company name is ignored: a Real Estate Admin uploads for their own company only
                ApartmentUpload.CompanyName = FixedCompanyName ?? string.Empty;
                ModelState.Remove("ApartmentUpload.CompanyName");
            }

            // Validate the specific input model for ApartmentUpload
            // This is crucial when you have multiple forms/handlers
            if (!TryValidateModel(ApartmentUpload))
            {
                ErrorMessage = "Please correct the errors in the Apartment Photo Upload form.";
                await LoadProjectsSelectListAsync(); // Reload dropdown for the other form
                return Page();
            }

            if (ApartmentUpload.Photos == null || !ApartmentUpload.Photos.Any())
            {
                ModelState.AddModelError("ApartmentUpload.Photos", "Please select at least one photo for the apartment.");
                ErrorMessage = "Please select at least one photo for the apartment.";
                await LoadProjectsSelectListAsync();
                return Page();
            }

            var invalid = ApartmentUpload.Photos.Select(p => SafeUpload.Validate(p, SafeUpload.ImageExtensions)).FirstOrDefault(e => e != null);
            if (invalid != null)
            {
                ModelState.AddModelError("ApartmentUpload.Photos", invalid);
                ErrorMessage = invalid;
                await LoadProjectsSelectListAsync();
                return Page();
            }

            try
            {
                // Same folder ApartmentHouses/Details reads from:
                // wwwroot/uploads/apartmenthousephotos/{first word of company name}/{N}bed/
                // Every segment is sanitised so user input can't escape the uploads folder.
                var companyFolder = (ApartmentUpload.CompanyName ?? string.Empty).Trim().Split(' ')[0];
                var bedroomCount = ApartmentUpload.BedroomCount;
                var uploadsFolder = SafeUpload.Under(
                    Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "apartmenthousephotos"),
                    companyFolder,
                    $"{bedroomCount}bed");

                Directory.CreateDirectory(uploadsFolder);

                foreach (var photo in ApartmentUpload.Photos)
                {
                    if (photo.Length > 0)
                    {
                        var filePath = Path.Combine(uploadsFolder, SafeUpload.FileName(photo, SafeUpload.ImageExtensions));

                        // Save the file to the determined path
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await photo.CopyToAsync(stream);
                        }
                    }
                }

                TempData["Success"] = $"Photos uploaded for {ApartmentUpload.CompanyName} ({ApartmentUpload.BedroomCount} bedrooms).";
                return RedirectToPage(); // post-redirect-get: a fresh, empty form
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred while uploading apartment photos: {ex.Message}";
                // Log the exception details for debugging purposes
                Console.WriteLine($"Error uploading apartment photos: {ex}");
            }

            await LoadProjectsSelectListAsync(); // Reload dropdown for the other form
            return Page();
        }

        // Handler for House Project Photo Upload Form
        public async Task<IActionResult> OnPostProjectPhotosAsync()
        {
            await LoadScopeAsync();
            if (NoCompany) return Page();

            // Validate the specific input model for ProjectUpload
            //if (!TryValidateModel(ProjectUpload))
            //{
            //    ErrorMessage = "Please correct the errors in the Project Photo Upload form.";
            //    await LoadProjectsSelectListAsync();
            //    return Page();
            //}

            // Explicitly check for empty ProjectId if it's required (as it is by [Required])
            if (ProjectUpload.ProjectId == Guid.Empty)
            {
                ModelState.AddModelError("ProjectUpload.ProjectId", "Please select a house project.");
                ErrorMessage = "Please select a house project.";
                await LoadProjectsSelectListAsync();
                return Page();
            }

            if (ProjectUpload.Photos == null || !ProjectUpload.Photos.Any())
            {
                ModelState.AddModelError("ProjectUpload.Photos", "Please select at least one photo for the project.");
                ErrorMessage = "Please select at least one photo for the project.";
                await LoadProjectsSelectListAsync();
                return Page();
            }

            var invalidProjectPhoto = ProjectUpload.Photos.Select(p => SafeUpload.Validate(p, SafeUpload.ImageExtensions)).FirstOrDefault(e => e != null);
            if (invalidProjectPhoto != null)
            {
                ModelState.AddModelError("ProjectUpload.Photos", invalidProjectPhoto);
                ErrorMessage = invalidProjectPhoto;
                await LoadProjectsSelectListAsync();
                return Page();
            }

            try
            {
                // Retrieve the project details from the database for folder naming
                // Only one of the user's own projects (any project for a Michu Admin)
                var houseProject = await _scope.Projects(_context.Projects).FirstOrDefaultAsync(p => p.ProjectId == ProjectUpload.ProjectId);
                if (houseProject == null)
                {
                    ModelState.AddModelError("ProjectUpload.ProjectId", "Selected project not found in the database.");
                    ErrorMessage = "Selected project not found.";
                    await LoadProjectsSelectListAsync();
                    return Page();
                }
                var owningCompany = await _context.HousingCompanies.FirstOrDefaultAsync(a => a.CompanyId == houseProject.HousingCompanyId);
                var projectOwningCompanyName = owningCompany?.Name?.Trim().Split(' ')[0];
                if (string.IsNullOrWhiteSpace(projectOwningCompanyName))
                {
                    ErrorMessage = "The selected project isn't linked to a housing company, so its photos can't be filed.";
                    await LoadProjectsSelectListAsync();
                    return Page();
                }
                else
                {
                    // wwwroot/uploads/HouseProjects/{first word of company name}/{ProjectId}/ (read by Projects/Details)
                    var uploadsFolder = SafeUpload.Under(
                        Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "HouseProjects"),
                        projectOwningCompanyName,
                        houseProject.ProjectId.ToString());
                    Directory.CreateDirectory(uploadsFolder);

                    foreach (var photo in ProjectUpload.Photos)
                    {
                        if (photo.Length > 0)
                        {
                            var filePath = Path.Combine(uploadsFolder, SafeUpload.FileName(photo, SafeUpload.ImageExtensions));

                            // Save the file
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await photo.CopyToAsync(stream);
                            }
                        }
                    }
                }
                TempData["Success"] = $"Photos uploaded for project {houseProject.ProjectName}.";
                return RedirectToPage(); // post-redirect-get: a fresh, empty form
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred while uploading project photos: {ex.Message}";
                // Log the exception details for debugging purposes
                Console.WriteLine($"Error uploading project photos: {ex}");
            }

            await LoadProjectsSelectListAsync(); // Always reload projects list to ensure it's up-to-date
            return Page();
        }

        // Helper method to populate the projects dropdown list
        private async Task LoadProjectsSelectListAsync()
        {
            var projects = await _scope.Projects(_context.Projects)
                .OrderBy(p => p.ProjectName)
                .Select(p => new
                {
                    p.ProjectId,
                    // Create a user-friendly display string for the project
                    FormattedProjectName = $"{p.ProjectName} (ID: {p.ProjectId.ToString().Substring(0, 8)}...)"
                })
                .ToListAsync();

            // Create the SelectList, binding ProjectId to value and FormattedProjectName to text
            ProjectsSelectList = new SelectList(projects, "ProjectId", "FormattedProjectName");
        }
    }

    // --- Input Models for Photo Upload Forms ---

    // Encapsulates properties for Apartment House photo uploads
    public class ApartmentUploadInputModel
    {
        [Required(ErrorMessage = "Company Name is required.")]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = string.Empty;

        [Range(1, 8, ErrorMessage = "Bedrooms must be between 1 and 8.")]
        [Display(Name = "Number of Bedrooms")]
        public int BedroomCount { get; set; } = 1; // Default to 1

        [Required(ErrorMessage = "Please select at least one photo.")]
        [Display(Name = "Apartment Photos")]
        [DataType(DataType.Upload)] // Hint for UI, not functional validation
        public List<IFormFile> Photos { get; set; } = new List<IFormFile>();
    }

    // Encapsulates properties for House Project photo uploads
    public class ProjectUploadInputModel
    {
        [Required(ErrorMessage = "Please select a house project.")]
        [Display(Name = "Select House Project")]
        public Guid ProjectId { get; set; } = Guid.Empty; // Guid.Empty represents "not selected"

        [Required(ErrorMessage = "Please select at least one photo.")]
        [Display(Name = "Project Photos")]
        [DataType(DataType.Upload)] // Hint for UI
        public List<IFormFile> Photos { get; set; } = new List<IFormFile>();
    }
}