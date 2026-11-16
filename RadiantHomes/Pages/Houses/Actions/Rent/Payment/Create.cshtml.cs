using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.DTOs;

namespace RadiantHomes.Pages.Houses.Actions.Rent.Payment
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly UserManager<AppUser> _userManager;

        public CommisionRate RentCommision { get; set; }
        public HouseRentTransaction RentTransaction { get; set; }
        public House House { get; set; } = new();
        public Customer HouseOwner { get; set; }
        public ChapaRequestDTO ChapaRequest { get; set; }
        public double RentedMonth { get; set; }

        [BindProperty]
        public HouseRentPayment HouseRentPayment { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public Guid TransactionId { get; set; }

        public CreateModel(ApplicationDbContext context, IConfiguration configuration, UserManager<AppUser> userManager)
        {
            _context = context;
            _configuration = configuration;
            _userManager = userManager;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (TransactionId == Guid.Empty)
            {
                ModelState.AddModelError("", "Invalid or missing transaction ID.");
                return Page();
            }

            RentTransaction = _context.HouseRentTransactions.Find(TransactionId);
            if (RentTransaction == null)
            {
                ModelState.AddModelError("", "Transaction not found.");
                return Page();
            }
            // Only a Michu admin or a party to the rent transaction (owner / tenant).
            if (!await TransactionAccess.CanAccessAsync(_userManager, User, RentTransaction.OwnerId, RentTransaction.CustomerId))
            {
                return Forbid();
            }

            House = _context.Houses.Find(RentTransaction.HouseId) ?? new();
            House.HouseRentInfo = new RentInfo();
            House.HouseRentInfo = _context.HouseRentInfos.FirstOrDefault(a => a.HouseId == House.Id);

            RentedMonth = ((RentTransaction.RentEndDate.Year - RentTransaction.RentStartingDate.Year) * 12) +
                          (RentTransaction.RentEndDate.Month - RentTransaction.RentStartingDate.Month);

            HouseRentPayment.RentPricePerTerm = House.HouseRentInfo?.PricePerTerm ?? 0;
            HouseRentPayment.TotalRentPrice = HouseRentPayment.RentPricePerTerm * RentedMonth;

            RentCommision = _context.CommisionRates
                .FirstOrDefault(a => a.CommisionRateDescription.ToLower() == "aaaaaaaa");

            if (RentCommision == null)
            {
                ModelState.AddModelError("", "Commission rate not found.");
                return Page();
            }

            HouseRentPayment.RentCommision = HouseRentPayment.TotalRentPrice * RentCommision.CommisionPercent;

            ChapaRequest = new ChapaRequestDTO(); // For form usage if needed

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            RentTransaction = _context.HouseRentTransactions.Find(TransactionId);
            if (RentTransaction == null)
            {
                ModelState.AddModelError("", "Transaction not found.");
                return Page();
            }
            // Checked before anything is sent to the payment provider.
            if (!await TransactionAccess.CanAccessAsync(_userManager, User, RentTransaction.OwnerId, RentTransaction.CustomerId))
            {
                return Forbid();
            }

            House = _context.Houses.Find(RentTransaction.HouseId) ?? new();
            House.HouseRentInfo = new RentInfo();
            House.HouseRentInfo = _context.HouseRentInfos.FirstOrDefault(a => a.HouseId == House.Id);
            RentedMonth = ((RentTransaction.RentEndDate.Year - RentTransaction.RentStartingDate.Year) * 12) +
                          (RentTransaction.RentEndDate.Month - RentTransaction.RentStartingDate.Month);

            RentCommision = _context.CommisionRates
                .FirstOrDefault(a => a.CommisionServiceName.Replace("_", " ") == "House Rent");

            if (RentCommision == null)
            {
                ModelState.AddModelError("", "Commission rate not found.");
                return Page();
            }
            HouseRentPayment.TransactionId = TransactionId;
            HouseRentPayment.RentPricePerTerm = House.HouseRentInfo?.PricePerTerm ?? 0;
            HouseRentPayment.TotalRentPrice = HouseRentPayment.RentPricePerTerm * RentedMonth;
            HouseRentPayment.RentCommision = HouseRentPayment.TotalRentPrice * RentCommision.CommisionPercent;

            ChapaRequest = new ChapaRequestDTO
            {
                Amount = ((RentCommision.CommisionPercent * HouseRentPayment.TotalRentPrice) / 100).ToString("F2"),
                Currency = "ETB",
                Email = User.Identity?.Name ?? "anonymous@radianthomes.com",
                first_name = User.Identity?.Name ?? "Anonymous",
                last_name = "User",
                tx_ref = Guid.NewGuid().ToString(),
                callback_url = $"{Request.Scheme}://{Request.Host}/Houses/Actions/Rent/Payment/Create",
                return_url = $"{Request.Scheme}://{Request.Host}/Houses/Actions/Rent/Payment/Create?tx_ref={Guid.NewGuid()}&houseId={House.Id}",
                customization = new Customization
                {
                    title = "Rent Payment",
                    description = "Pay to see contact details"
                }
            };

            using var httpClient = new HttpClient();
            // Chapa secret key comes from configuration (user-secrets locally, Chapa__SecretKey env var on servers)
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_configuration["Chapa:SecretKey"]}");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await httpClient.PostAsJsonAsync("https://api.chapa.co/v1/transaction/initialize", ChapaRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Chapa API Response: {responseContent}");
            Console.WriteLine("StatusCode: " + response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                AuditFields.ResetForInsert(HouseRentPayment); // stamped by ApplicationDbContext on save
                _context.HouseRentPayments.Add(HouseRentPayment);
                await _context.SaveChangesAsync();
                return RedirectToPage("./Index");
            }

            ModelState.AddModelError("", $"{responseContent} Failed to initiate payment.");
            return Page();
        }
    }
}
