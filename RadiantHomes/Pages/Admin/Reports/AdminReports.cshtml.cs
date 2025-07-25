using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using RadiantHomes.DAL;
using SQLitePCL;

// Assume HouseSalesExportHelper is a static class with static methods
// public static class HouseSalesExportHelper {
//     public static byte[] ToPdfHouseSold<T>(IEnumerable<T> data) { /* ... */ }
//     public static byte[] ToExcelHouseSold<T>(IEnumerable<T> data) { /* ... */ }
//     // Add specific methods for different report types if needed, or keep generic
// }


namespace RadiantHomes.Pages.Admin.Reports
{
    public class AdminReportsModel : PageModel
    {
        private readonly IAdminDashboard _adminDashboardService;
        private readonly ApplicationDbContext _context;

        // Constructor: No need to inject HouseSalesExportHelper if it's static
        public AdminReportsModel(ApplicationDbContext dbContext, IAdminDashboard adminDashboardService)
        {
            _adminDashboardService = adminDashboardService;
            _context = dbContext;
        }

        [BindProperty]
        public string ReportType { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        // Properties to hold report data for display
        public List<ApartmentHouse> HouseReportData { get; set; } = new();
        public List<Customer> UsersReportData { get; set; } = new();
        public List<ApartmentHouseSaleTransaction> HouseSoldTransactions { get; set; } = new();
        public List<HousingCompany> RealEstatesRegistered { get; set; } = new();

        // If you're going to have an "All Users (Agents)" report distinct from "All Customers"
        // and it returns a different model (e.g., AppUser), you'd need another property:
        // public List<AppUser> AllAgentsData { get; set; } = new();


        public void OnGet()
        {
            // Initializing in property declarations is cleaner, but this is fine too.
        }

        #region Download Handlers

        // Handler for "Apartment/House Sale Transactions" PDF
        public IActionResult OnGetDownloadHouseSoldPdf()
        {
            var dataToExport = _adminDashboardService.ApartmentHouseSaleTransactions(true); // Re-fetch data

            if (dataToExport == null || !dataToExport.Any())
            {
                TempData["ErrorMessage"] = "No data available to generate PDF for House Sale Transactions.";
                return RedirectToPage();
            }

            try
            {
                List<HouseSalesReport> salesReport = new();
                foreach(var housedata in dataToExport)
                {
                    var HouseOwner = _context.UserProfiles.Where(a => a.Id == housedata.OwnerId).FirstOrDefault()??new();
                    var HouseOwnerName = HouseOwner.FirstName + " " + HouseOwner.LastName;
                    var HouseBuyer = _context.UserProfiles.Where(a => a.Id == housedata.CustomerId).FirstOrDefault() ?? new();
                    var HouseBuyerName = HouseBuyer.FirstName + " " + HouseBuyer.LastName;
                    salesReport.Add(new HouseSalesReport() { BuyerName = HouseBuyerName, SellerName = HouseOwnerName, DateSold = housedata.CreatedOn, HouseId = housedata.ApartmentHouseId, Notes = housedata.Notes });

                }
                // Call static method directly
                var bytes = HouseSalesExportHelper.ToPdfHouseSold(salesReport, "House Sales Report");
                return File(bytes, "application/pdf", $"HouseSalesTransactionsReport_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating PDF for House Sale Transactions: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Handler for "Apartment/House Sale Transactions" Excel
        public IActionResult OnGetDownloadHouseSoldExcel()
        {
            var dataToExport = _adminDashboardService.ApartmentHouseSaleTransactions(true);

            if (dataToExport == null || !dataToExport.Any())
            {
                TempData["ErrorMessage"] = "No data available to generate Excel for House Sale Transactions.";
                return RedirectToPage();
            }

            try
            {
                // Call static method directly
                var bytes = HouseSalesExportHelper.ToPdfHouseSold(dataToExport,""); // You need to implement ToExcelHouseSold
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"HouseSalesTransactionsReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating Excel for House Sale Transactions: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Handler for "All Apartment Houses" PDF
        public IActionResult OnGetDownloadApartmentHousePdf()
        {
            var dataToExport = _adminDashboardService.GetAllApartmentHouses();

            if (dataToExport == null || !dataToExport.Any())
            {
                TempData["ErrorMessage"] = "No data available to generate PDF for All Apartment Houses.";
                return RedirectToPage();
            }

            try
            {
                // Call static method directly
                var bytes = HouseSalesExportHelper.ToPdfHouseSold(dataToExport, ""); // Re-use generic PDF helper
                return File(bytes, "application/pdf", $"AllApartmentHousesReport_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating PDF for All Apartment Houses: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Handler for "All Apartment Houses" Excel
        public IActionResult OnGetDownloadApartmentHouseExcel()
        {
            var dataToExport = _adminDashboardService.GetAllApartmentHouses();

            if (dataToExport == null || !dataToExport.Any())
            {
                TempData["ErrorMessage"] = "No data available to generate Excel for All Apartment Houses.";
                return RedirectToPage();
            }

            try
            {
                // Call static method directly
                var bytes = HouseSalesExportHelper.ToPdfHouseSold(dataToExport); // Re-use generic Excel helper
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"AllApartmentHousesReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating Excel for All Apartment Houses: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Handler for "Housing Companies" PDF
        public IActionResult OnGetDownloadHousingCompaniesPdf()
        {
            var dataToExport = _adminDashboardService.GetHousingCompanies();

            if (dataToExport == null || !dataToExport.Any())
            {
                TempData["ErrorMessage"] = "No data available to generate PDF for Housing Companies.";
                return RedirectToPage();
            }

            try
            {
                // Call static method directly
                var bytes = HouseSalesExportHelper.ToPdfHouseSold(dataToExport); // Re-use generic PDF helper
                return File(bytes, "application/pdf", $"HousingCompaniesReport_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating PDF for Housing Companies: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Handler for "Housing Companies" Excel
        public IActionResult OnGetDownloadHousingCompaniesExcel()
        {
            var dataToExport = _adminDashboardService.GetHousingCompanies();

            if (dataToExport == null || !dataToExport.Any())
            {
                TempData["ErrorMessage"] = "No data available to generate Excel for Housing Companies.";
                return RedirectToPage();
            }

            try
            {
                // Call static method directly
                var bytes = HouseSalesExportHelper.ToPdfHouseSold(dataToExport); // Re-use generic Excel helper
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"HousingCompaniesReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating Excel for Housing Companies: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Handler for "All Customers" PDF
        public IActionResult OnGetDownloadAllCustomersPdf()
        {
            var dataToExport = _adminDashboardService.GetAllCustomers();

            if (dataToExport == null || !dataToExport.Any())
            {
                TempData["ErrorMessage"] = "No data available to generate PDF for All Customers.";
                return RedirectToPage();
            }

            try
            {
                // Call static method directly
                var bytes = HouseSalesExportHelper.ToPdfHouseSold(dataToExport); // Re-use generic PDF helper
                return File(bytes, "application/pdf", $"AllCustomersReport_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating PDF for All Customers: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Handler for "All Customers" Excel
        public IActionResult OnGetDownloadAllCustomersExcel()
        {
            var dataToExport = _adminDashboardService.GetAllCustomers();

            if (dataToExport == null || !dataToExport.Any())
            {
                TempData["ErrorMessage"] = "No data available to generate Excel for All Customers.";
                return RedirectToPage();
            }

            try
            {
                // Call static method directly
                var bytes = HouseSalesExportHelper.ToPdfHouseSold(dataToExport); // Re-use generic Excel helper
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"AllCustomersReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating Excel for All Customers: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Handler for "Sold Houses by Date Range" PDF
        public IActionResult OnGetDownloadSoldHousesByDateRangePdf()
        {
            if (!StartDate.HasValue || !EndDate.HasValue)
            {
                TempData["ErrorMessage"] = "Start Date and End Date are required to download Sold Houses by Date Range report.";
                return RedirectToPage();
            }

            var dataToExport = _adminDashboardService.GetSoldHouses(companyId: null, StartDate.Value, EndDate.Value, isSuperAdmin: true);

            if (dataToExport == null || !dataToExport.Any())
            {
                TempData["ErrorMessage"] = "No data available to generate PDF for Sold Houses by Date Range.";
                return RedirectToPage();
            }

            try
            {
                // Call static method directly
                var bytes = HouseSalesExportHelper.ToPdfHouseSold(dataToExport);
                return File(bytes, "application/pdf", $"SoldHousesByDateRangeReport_{StartDate.Value:yyyyMMdd}-{EndDate.Value:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating PDF for Sold Houses by Date Range: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Handler for "Sold Houses by Date Range" Excel
        public IActionResult OnGetDownloadSoldHousesByDateRangeExcel()
        {
            if (!StartDate.HasValue || !EndDate.HasValue)
            {
                TempData["ErrorMessage"] = "Start Date and End Date are required to download Sold Houses by Date Range report.";
                return RedirectToPage();
            }

            var dataToExport = _adminDashboardService.GetSoldHouses(companyId: null, StartDate.Value, EndDate.Value, isSuperAdmin: true);

            if (dataToExport == null || !dataToExport.Any())
            {
                TempData["ErrorMessage"] = "No data available to generate Excel for Sold Houses by Date Range.";
                return RedirectToPage();
            }

            try
            {
                // Call static method directly
                var bytes = HouseSalesExportHelper.ToPdfHouseSold(dataToExport);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"SoldHousesByDateRangeReport_{StartDate.Value:yyyyMMdd}-{EndDate.Value:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating Excel for Sold Houses by Date Range: {ex.Message}";
                return RedirectToPage();
            }
        }

        // Add Handlers for "All Users (Agents)" if different from Customers
        // public IActionResult OnGetDownloadAllUsersPdf() { ... }
        // public IActionResult OnGetDownloadAllUsersExcel() { ... }

        #endregion

        public async Task<IActionResult> OnPostAsync()
        {
            // Initializing in property declarations is better, but doing it here ensures fresh lists on each POST
            HouseReportData = new();
            RealEstatesRegistered = new();
            HouseSoldTransactions = new();
            UsersReportData = new();
            // AllAgentsData = new(); // If you add this property for agents

            switch (ReportType)
            {
                case "ApartmentHouseSaleTransactions":
                    HouseSoldTransactions = _adminDashboardService.ApartmentHouseSaleTransactions(true);
                    break;

                case "AllApartmentHouses":
                    HouseReportData = _adminDashboardService.GetAllApartmentHouses();
                    break;

                case "AllCustomers":
                    UsersReportData = _adminDashboardService.GetAllCustomers();
                    break;

                case "AllUsers":
                    // IMPORTANT: You need to decide what 'AllUsers' means.
                    // If it's your application's users (e.g., agents, admins),
                    // you likely have a different model (e.g., AppUser).
                    // If so, you'll need:
                    // 1. A new property in this model (e.g., public List<AppUser> AllAgents { get; set; }).
                    // 2. A method in IAdminDashboard to get these users (e.g., GetAllAppUsers()).
                    // 3. Populate that new property here.
                    // For now, I'm keeping your ModelState.AddModelError.
                    ModelState.AddModelError("", "Report 'All Users (Agents)' data retrieval not yet fully implemented. Please select another report type.");
                    break;

                case "HousingCompanies":
                    RealEstatesRegistered = _adminDashboardService.GetHousingCompanies();
                    break;

                case "SoldHousesByDateRange":
                    if (StartDate.HasValue && EndDate.HasValue)
                    {
                        // Note: This populates HouseReportData as per your Razor logic for this report
                        HouseReportData = _adminDashboardService.GetSoldHouses(companyId: null, StartDate.Value, EndDate.Value, isSuperAdmin: true);
                    }
                    else
                    {
                        ModelState.AddModelError("", "Start Date and End Date are required for Sold Houses by Date Range report.");
                    }
                    break;

                default:
                    ModelState.AddModelError("", "Please select a valid report type to generate.");
                    break;
            }

            return Page(); // Stay on the same page and display the results
        }
    }
    public class HouseSalesReport
    {
        public string BuyerName { get; set; }
        public string SellerName { get; set; }
        public string Notes { get; set; }
        public DateTime DateSold { get; set; }
        public Guid HouseId { get; set; }
    }
}