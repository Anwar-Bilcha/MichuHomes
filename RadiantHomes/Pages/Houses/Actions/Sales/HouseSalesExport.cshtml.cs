using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using RadiantHomes.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using RadiantHomes.Models;
using RadiantHomes.Pages.Houses;

public class HouseSalesExportModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<AppUser> _userManager;

    public HouseSalesExportModel(ApplicationDbContext context, UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> OnGetAsync(string format)
    {
        // Same rows as the sales list: everything for a Michu admin, otherwise only the user's own sales.
        var customerId = await HouseAccess.CurrentCustomerIdAsync(_userManager, User);
        var sales = await _context.HouseSaleTransactions
            .VisibleTo(TransactionAccess.IsAdmin(User), customerId)
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
