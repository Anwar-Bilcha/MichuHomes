using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages.Buildings
{
    public class DownloadBuildingTemplateModel : PageModel
    {
        public IActionResult OnGet()
        {
           
            //var exporter = new ExcelExportService();
            var fileBytes = ExcelExportService.GenerateEmptyBuildingTemplate();

            return File(fileBytes,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "BuildingTemplate.xlsx");
        }
    }
}
