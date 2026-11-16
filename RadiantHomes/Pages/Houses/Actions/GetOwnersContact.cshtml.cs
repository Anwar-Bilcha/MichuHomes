using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Services;

namespace RadiantHomes.Pages.Houses.Actions
{
    /// <summary>
    /// Sends the owner's contact details of a house by SMS to the signed-in user's own phone number.
    /// </summary>
    [Authorize]
    public class GetOwnersContactModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ISmsService _smsService;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<GetOwnersContactModel> _logger;
        private readonly RadiantHomes.Localization.TranslationCatalog _catalog;

        public House House { get; set; } = default!;
        public Customer Owner { get; set; } = default!;

        /// <summary>The signed-in user's phone number the SMS goes to (null when none is on file).</summary>
        public string? RecipientPhone { get; set; }

        public string? ContactRequestMessage { get; set; }

        /// <summary>The signed-in user owns the house: there is no point texting them their own contact.</summary>
        public bool IsOwnHouse { get; set; }
        public bool ContactRequestSucceeded { get; set; }

        public GetOwnersContactModel(ApplicationDbContext context, ISmsService smsService,
            UserManager<AppUser> userManager, ILogger<GetOwnersContactModel> logger,
            RadiantHomes.Localization.TranslationCatalog catalog)
        {
            _context = context;
            _smsService = smsService;
            _userManager = userManager;
            _logger = logger;
            _catalog = catalog;
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            if (!await LoadHouseAndOwnerAsync(id))
            {
                return NotFound();
            }

            RecipientPhone = await GetSignedInUserPhoneAsync();
            await CheckOwnHouseAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            if (!await LoadHouseAndOwnerAsync(id))
            {
                return NotFound();
            }

            RecipientPhone = await GetSignedInUserPhoneAsync();
            if (await CheckOwnHouseAsync())
            {
                return Page(); // nothing is sent for the user's own house
            }
            if (RecipientPhone is null)
            {
                ContactRequestMessage = "We couldn't send the owner's contact because there is no phone number on your profile. " +
                                        "Add your phone number to your account, then try again.";
                return Page();
            }

            // The SMS goes out in the language the user is browsing in (labels only; names stay as entered).
            var culture = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            string L(string label) => _catalog.T(label, culture);
            var messageBody = $"{L("House")}: {House.Title}, {L("City")}: {House.HouseAddress?.City}. " +
                              $"{L("Owner")}: {Owner.FirstName} {Owner.LastName}, {L("Phone")}: {Owner.PhoneNumber}, {L("Email")}: {Owner.Email}";
            try
            {
                await _smsService.SendMessageAsync(messageBody, RecipientPhone);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sending the owner's contact for house {HouseId} by SMS failed", House.Id);
                ContactRequestMessage = "We couldn't send the text message right now. Please try again later.";
                return Page();
            }

            ContactRequestSucceeded = true;
            ContactRequestMessage = $"The owner's contact information for '{House.Title}' has been sent by text message to your phone number {RecipientPhone}.";
            return Page();
        }

        /// <summary>Sets <see cref="IsOwnHouse"/> and the message shown instead of the request button.</summary>
        private async Task<bool> CheckOwnHouseAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            IsOwnHouse = user?.CustomerId is Guid customerId && customerId == House.HouseOwnerId;
            if (IsOwnHouse)
            {
                ContactRequestMessage = "This is your own house, so there is no owner's contact to request.";
            }
            return IsOwnHouse;
        }

        private async Task<bool> LoadHouseAndOwnerAsync(Guid id)
        {
            var house = await _context.Houses
                .AsNoTracking()
                .Include(h => h.HouseAddress)
                .FirstOrDefaultAsync(h => h.Id == id);
            if (house is null)
            {
                return false;
            }

            var owner = await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == house.HouseOwnerId);
            if (owner is null)
            {
                return false;
            }

            House = house;
            Owner = owner;
            return true;
        }

        /// <summary>
        /// The signed-in user's phone: their customer profile (AppUser.CustomerId → UserProfiles.PhoneNumber) first,
        /// then the phone number on the login account (AppUser.PhoneNumber). Returned in international format.
        /// </summary>
        private async Task<string?> GetSignedInUserPhoneAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return null;
            }

            string? phone = null;
            if (user.CustomerId is Guid customerId && customerId != Guid.Empty)
            {
                phone = await _context.UserProfiles.AsNoTracking()
                    .Where(c => c.Id == customerId)
                    .Select(c => c.PhoneNumber)
                    .FirstOrDefaultAsync();
            }
            if (string.IsNullOrWhiteSpace(phone))
            {
                phone = user.PhoneNumber;
            }

            return ToInternationalFormat(phone);
        }

        /// <summary>
        /// Profiles store local Ethiopian numbers ("0912345678"); the SMS provider needs E.164 ("+251912345678").
        /// </summary>
        internal static string? ToInternationalFormat(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }

            var hasPlus = phone.TrimStart().StartsWith('+');
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
            {
                return null;
            }

            if (hasPlus) return "+" + digits;
            if (digits.StartsWith("251") && digits.Length == 12) return "+" + digits;
            if (digits.StartsWith('0') && digits.Length == 10) return "+251" + digits[1..];
            if (digits.Length == 9 && (digits[0] == '9' || digits[0] == '7')) return "+251" + digits;
            return "+" + digits;
        }
    }
}
