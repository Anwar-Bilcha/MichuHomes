using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using RadiantHomes.DAL;
using Microsoft.EntityFrameworkCore;

public class HouseSalesExportModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public HouseSalesExportModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> OnGetAsync(string format)
    {
        var sales = await _context.HouseSaleTransactions
            .Include(a=>a.House).Include(a=>a.Receiver).Include(a=>a.HomeOwner).ToListAsync();

        if (format?.ToLower() == "excel")
        {
            var bytes = HouseSalesExportHelper.ToExcel(sales);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "HouseSales.xlsx");
        }
        else if (format?.ToLower() == "pdf")
        {
            var bytes = HouseSalesExportHelper.ToPdf(sales);
            return File(bytes, "application/pdf", "HouseSales.pdf");
        }

        return RedirectToPage("./Index");
    }
}
