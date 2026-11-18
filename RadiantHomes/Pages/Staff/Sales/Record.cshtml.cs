using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Sales;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Services.CustomerPortal;
using RadiantHomes.Services.Sales;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Sales
{
    /// <summary>
    /// Record the sale of a reserved home (?id = the reservation): agreed price, discount, sale date, selling agent,
    /// payment status and the signed agreement. The home becomes sold, the customer is congratulated and their lead
    /// is converted.
    /// </summary>
    public class RecordModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly ReservationService _reservations;
        private readonly SaleAgreements _agreements;
        private readonly SalesReports _reports;
        private readonly AuditService _audit;

        public RecordModel(ApplicationDbContext db, ReservationService reservations, SaleAgreements agreements, SalesReports reports, AuditService audit)
        {
            _db = db;
            _reservations = reservations;
            _agreements = agreements;
            _reports = reports;
            _audit = audit;
        }

        [BindProperty(SupportsGet = true)] public Guid Id { get; set; }

        [BindProperty, Required(ErrorMessage = "Enter the agreed price."), Range(1, double.MaxValue, ErrorMessage = "Enter the agreed price.")]
        [Display(Name = "Agreed price")]
        public double? AgreedPrice { get; set; }

        [BindProperty, Range(0, double.MaxValue, ErrorMessage = "The discount can't be negative.")]
        [Display(Name = "Discount (optional)")]
        public double? Discount { get; set; }

        [BindProperty, Required(ErrorMessage = "Enter the sale date."), DataType(DataType.Date)]
        [Display(Name = "Sale date")]
        public DateTime? SoldOn { get; set; }

        [BindProperty, Display(Name = "Sold by")]
        public string? SoldByUserId { get; set; }

        [BindProperty, Required, Display(Name = "Payment")]
        public string PaymentStatus { get; set; } = "Deposit";

        [BindProperty, Display(Name = "Signed agreement (PDF, JPG or PNG, optional)")]
        public IFormFile? Agreement { get; set; }

        public ApartmentHouseSaleTransaction Sale { get; private set; } = default!;
        public List<(string Id, string Name)> Staff { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var access = await HttpContext.AccessAsync();
            if (await LoadAsync() is not { } sale) return NotFound();
            await OptionsAsync(sale);
            AgreedPrice = sale.ApartmentHouse.SalePrice > 0 ? sale.ApartmentHouse.SalePrice : null;
            SoldOn = DateTime.Today;
            var leadAgent = sale.LeadId is Guid leadId ? await _db.Leads.Where(l => l.Id == leadId).Select(l => l.AgentUserId).FirstOrDefaultAsync() : null;
            SoldByUserId = leadAgent ?? (Staff.Any(s => s.Id == access.UserId) ? access.UserId : null);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var access = await HttpContext.AccessAsync();
            if (await LoadAsync() is not { } sale) return NotFound();
            if (Agreement is not null && SaleAgreements.Check(Agreement) is { } fileProblem) ModelState.AddModelError(nameof(Agreement), fileProblem);
            if (!ModelState.IsValid)
            {
                await OptionsAsync(sale);
                return Page();
            }

            var scope = await CompanyScope.ForAsync(User, _db);
            var details = new SaleDetails(AgreedPrice!.Value, Discount, SoldOn!.Value, string.IsNullOrWhiteSpace(SoldByUserId) ? null : SoldByUserId, PaymentStatus);
            var result = await _reservations.ChangeStatusAsync(sale.Id, HouseReserveStatuses.Completed, scope, null, access.UserId, User.Identity?.Name, details);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                await OptionsAsync(sale);
                return Page();
            }

            var stored = await _db.ApartmentHouseSaleTransactions.FirstAsync(t => t.Id == sale.Id);
            if (Agreement is not null) stored.AgreementFileName = await _agreements.SaveAsync(sale.Id, Agreement);
            _audit.Add(AuditCategories.Sales, "SaleRecorded",
                $"Sale {stored.Reference} recorded: {details.AgreedPrice:N0} {sale.ApartmentHouse.Currency}{(details.Discount is > 0 ? $" (discount {details.Discount:N0})" : "")}, {PaymentStatuses.Label(details.PaymentStatus)}.",
                stored.CompanyId, nameof(ApartmentHouseSaleTransaction), stored.Id.ToString());
            await _db.SaveChangesAsync();
            TempData["Success"] = result.Message;
            return RedirectToPage("./Index");
        }

        private async Task<ApartmentHouseSaleTransaction?> LoadAsync()
        {
            var scope = await CompanyScope.ForAsync(User, _db);
            var sale = await _db.ApartmentHouseSaleTransactions.AsNoTracking()
                .Include(t => t.ApartmentHouse).ThenInclude(a => a.Building!).ThenInclude(b => b.Project!).ThenInclude(p => p.Company)
                .Include(t => t.Receiver)
                .FirstOrDefaultAsync(t => t.Id == Id);
            if (sale is null || !scope.CanAccess(sale.ApartmentHouse?.Building?.Project?.HousingCompanyId)) return null;
            if (sale.Status is not (HouseReserveStatuses.Pending or HouseReserveStatuses.Approved)) return null;
            Sale = sale;
            return sale;
        }

        private async Task OptionsAsync(ApartmentHouseSaleTransaction sale)
        {
            Sale = sale;
            var companyId = sale.ApartmentHouse.Building!.Project!.HousingCompanyId;
            var ids = await _db.AppUsers.AsNoTracking().Where(u => u.AccountType == AccountType.CompanyStaff && u.HousingCompanyId == companyId).Select(u => u.Id).ToListAsync();
            var names = await _reports.NamesAsync(ids);
            Staff = ids.Select(id => (id, names.GetValueOrDefault(id, id))).OrderBy(s => s.Item2).ToList();
        }
    }
}
