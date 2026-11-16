using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.DAL;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.Admin.Reports
{
    public class AdminReportsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminReportsModel> _logger;

        public AdminReportsModel(ApplicationDbContext dbContext, ILogger<AdminReportsModel> logger)
        {
            _context = dbContext;
            _logger = logger;
        }

        [BindProperty]
        public string? ReportType { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        // The generated report (null until one is generated). The downloads export exactly these columns and rows.
        public AdminReport? Report { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var (report, error) = await AdminReports.BuildAsync(_context, ReportType, StartDate, EndDate);
            if (error != null)
            {
                ModelState.AddModelError(string.Empty, error);
            }
            Report = report;
            return Page(); // Stay on the same page and display the results
        }

        // Downloads a report as PDF (format=pdf) or Excel (format=xlsx). The date range comes from StartDate/EndDate.
        public async Task<IActionResult> OnGetDownloadAsync(string? report, string? format)
        {
            var isPdf = string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);
            var isXlsx = string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase);
            if (!isPdf && !isXlsx)
            {
                TempData["ErrorMessage"] = "Please choose PDF or Excel as the download format.";
                return RedirectToPage();
            }

            var (built, error) = await AdminReports.BuildAsync(_context, report, StartDate, EndDate);
            if (built == null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToPage();
            }
            if (built.Rows.Count == 0)
            {
                TempData["ErrorMessage"] = $"No data available to download for {built.Title}.";
                return RedirectToPage();
            }

            try
            {
                return isPdf
                    ? File(built.ToPdf(), AdminReport.PdfContentType, $"{built.FileName}.pdf")
                    : File(built.ToXlsx(), AdminReport.XlsxContentType, $"{built.FileName}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not generate the {Format} file for report {Report}", isPdf ? "PDF" : "Excel", built.Key);
                TempData["ErrorMessage"] = $"The {(isPdf ? "PDF" : "Excel")} file for {built.Title} could not be generated. Please try again.";
                return RedirectToPage();
            }
        }
    }
}
