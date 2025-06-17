using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

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
        public async Task<IActionResult> OnPostUploadBuildingsAsync()
        {

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                // Load the Excel file using ClosedXML
                stream.Position = 0;
                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheets.Worksheet(1); // Get the first worksheet
                    int lastRow = worksheet.LastRowUsed().RowNumber();

                    // Loop through rows in Excel sheet
                    for (int row = 2; row <= lastRow; row++)
                    {
                        var projectName = worksheet.Cell(row, 1).GetString(); // Assuming ProjectName is in the first column
                        Guid projectId = await GetProjectIdByNameAsync(projectName);

                        if (projectId != Guid.Empty)
                        {
                            var proJect = _context.Projects.FirstOrDefault(a => a.ProjectId == projectId) ?? new();
                            var building = new Building
                            {
                                ProjectId = projectId,
                                BuildingName = worksheet.Cell(row, 2).GetString(), // Adjust column indexes based on your Excel file
                                NumberOfFloors = int.Parse(worksheet.Cell(row, 3).GetString()),
                                BuildingType = worksheet.Cell(row, 4).GetString(),
                                ConstructionStatus = worksheet.Cell(row, 5).GetString(),
                                CreatedBy = "Admin", // Or fetch from session/user context
                                CreatedOn = DateTime.Now,
                                UpdatedBy = "Admin",
                                UpdatedOn = DateTime.Now,
                                AddressId = proJect.AddressId
                            };

                            _context.Buildings.Add(building);
                            await _context.SaveChangesAsync();

                        }
                    }

                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToPage("./Index");
        }

        public IActionResult OnGetDownloadTemplate()
        {
            var filePath = Path.Combine(_env.WebRootPath, "templates", "BuildingTemplate.xlsx");
            var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return PhysicalFile(filePath, contentType, "BuildingTemplate.xlsx");
        }

        public IActionResult OnGet(Guid projectId)
        {

            ViewData["ProjectId"] = new SelectList(_context.Projects, "ProjectId", "ProjectName");
            return Page();
        }

        [BindProperty]
        public Building Building { get; set; } = new Building();

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            //if (!ModelState.IsValid)
            //{
            //    return Page();
            //}
            Building.ProjectId = ProjectId;
            _context.Buildings.Add(Building);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
        public async Task<Guid> GetProjectIdByNameAsync(string projectName)
        {
            var project = _context.Projects
        .AsEnumerable() // 👈 Forces EF to switch from SQL to LINQ-to-Objects
        .FirstOrDefault(p =>
            p.ProjectName?.Trim().Equals(projectName.Trim(), StringComparison.OrdinalIgnoreCase) == true);

            return project?.ProjectId ?? Guid.Empty;
        }

    }
}
