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

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        // --- Apartment House Photo Upload Properties ---
        [BindProperty]
        public ApartmentUploadInputModel ApartmentUpload { get; set; } = new ApartmentUploadInputModel();

        // --- House Project Photo Upload Properties ---
        [BindProperty]
        public ProjectUploadInputModel ProjectUpload { get; set; } = new ProjectUploadInputModel();

        // SelectList for House Projects dropdown
        public SelectList? ProjectsSelectList { get; set; }

        public async Task OnGetAsync()
        {
            // Initialize input models to ensure they are not null when the page loads
            ApartmentUpload = new ApartmentUploadInputModel();
            ProjectUpload = new ProjectUploadInputModel();

            // Load House Projects for the dropdown
            await LoadProjectsSelectListAsync();
        }

        // Handler for Apartment House Photo Upload Form
        public async Task<IActionResult> OnPostApartmentPhotosAsync()
        {
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

            try
            {
                // Sanitize company name for folder paths (replace spaces with underscores)
                var companyName = ApartmentUpload.CompanyName.Replace(" ", "_");
                var bedroomCount = ApartmentUpload.BedroomCount;

                // Define the upload path: wwwroot/uploads/ApartmentHouses/{CompanyName}/{BedroomCount}/
                var uploadsFolder = Path.Combine(
                    _webHostEnvironment.WebRootPath,
                    "uploads",
                    "ApartmentHouses",
                    companyName,
                    bedroomCount.ToString()
                );

                // Create directory if it doesn't exist
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                foreach (var photo in ApartmentUpload.Photos)
                {
                    if (photo.Length > 0)
                    {
                        var fileName = Path.GetFileNameWithoutExtension(photo.FileName);
                        var fileExtension = Path.GetExtension(photo.FileName);
                        // Append GUID to filename to ensure uniqueness and prevent overwrites
                        var newFileName = $"{fileName}_{Guid.NewGuid()}{fileExtension}";
                        var filePath = Path.Combine(uploadsFolder, newFileName);

                        // Save the file to the determined path
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await photo.CopyToAsync(stream);
                        }
                    }
                }

                SuccessMessage = $"Successfully uploaded photos for {ApartmentUpload.CompanyName} ({ApartmentUpload.BedroomCount} bedrooms).";
                ApartmentUpload = new ApartmentUploadInputModel(); // Clear the form after successful upload
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

            try
            {
                // Retrieve the project details from the database for folder naming
                var houseProject = await _context.Projects.FindAsync(ProjectUpload.ProjectId);
                if (houseProject == null)
                {
                    ModelState.AddModelError("ProjectUpload.ProjectId", "Selected project not found in the database.");
                    ErrorMessage = "Selected project not found.";
                    await LoadProjectsSelectListAsync();
                    return Page();
                }
                var projectOwningCompanyName = _context.HousingCompanies.FirstOrDefault(a => a.CompanyId == houseProject.HousingCompanyId).Name.Split(" ")[0]??" ";
                if (!string.IsNullOrWhiteSpace(projectOwningCompanyName))
                {
                    var projectNameFolder = houseProject.ProjectName.Replace(" ", "_").Replace("/", "-");
                    // Define the upload path: wwwroot/uploads/HouseProjects/{ProjectId}_{ProjectName}/
                    var uploadsFolder = Path.Combine(
                        _webHostEnvironment.WebRootPath,
                        "uploads",
                        "HouseProjects",
                        projectOwningCompanyName,
                        $"{houseProject.ProjectId}"
                    );
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }


                    // Sanitize project name for path to avoid invalid characters


                    // Create directory if it doesn't exist
                    foreach (var photo in ProjectUpload.Photos)
                    {
                        if (photo.Length > 0)
                        {
                            var fileName = Path.GetFileNameWithoutExtension(photo.FileName);
                            var fileExtension = Path.GetExtension(photo.FileName);
                            var newFileName = $"{fileName}_{Guid.NewGuid()}{fileExtension}"; // Ensure unique file names
                            var filePath = Path.Combine(uploadsFolder, newFileName);

                            // Save the file
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await photo.CopyToAsync(stream);
                            }
                        }
                    }
                }
                SuccessMessage = $"Successfully uploaded photos for Project: {houseProject.ProjectName}.";
                ProjectUpload = new ProjectUploadInputModel(); // Clear the form after successful upload
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
            var projects = await _context.Projects
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