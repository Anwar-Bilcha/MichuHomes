using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Services.CustomerPortal
{
    /// <summary>A friend who joined with one of the customer's codes, as the portal shows them (no full e-mail).</summary>
    public sealed record ReferredFriend(string Name, DateTime JoinedOn, string Code, string Status);

    /// <summary>
    /// "Refer a friend": customers generate codes (Models/Referrals.cs) and share the link /r/{code}. The link keeps
    /// the code in a cookie for 30 days and opens the registration page; a new account registered with it becomes a
    /// Referral and the customer is notified.
    /// </summary>
    public sealed partial class ReferralService
    {
        public const string CookieName = "michu.ref";
        public const int MaxActiveCodes = 5;
        public const int MaxLabelLength = 60;
        private const string Prefix = "MH-";
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O or 1/I to mix up

        private readonly ApplicationDbContext _context;
        private readonly CustomerNotifications _customers;

        public ReferralService(ApplicationDbContext context, CustomerNotifications customers)
        {
            _context = context;
            _customers = customers;
        }

        /// <summary>"mh-7k3q 9p" → "MH-7K3Q9P"; anything that can't be a code → empty.</summary>
        public static string Normalize(string? code)
        {
            var cleaned = Regex.Replace(code ?? string.Empty, @"\s+", string.Empty).ToUpperInvariant();
            if (!cleaned.StartsWith(Prefix) && cleaned.StartsWith("MH")) cleaned = Prefix + cleaned[2..];
            return CodePattern().IsMatch(cleaned) ? cleaned : string.Empty;
        }

        public static string LinkPath(string code) => $"/r/{code}";

        public async Task<(ReferralCode? Code, string? Error)> GenerateAsync(Guid customerId, string? label)
        {
            var active = await _context.ReferralCodes.CountAsync(c => c.CustomerId == customerId && c.IsActive);
            if (active >= MaxActiveCodes) return (null, $"You can have at most {MaxActiveCodes} active codes. Turn one off first.");
            label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
            if (label?.Length > MaxLabelLength) return (null, $"The label can be at most {MaxLabelLength} characters.");

            for (var attempt = 0; attempt < 10; attempt++)
            {
                var code = Prefix + new string(Enumerable.Range(0, 6).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());
                if (await _context.ReferralCodes.AnyAsync(c => c.Code == code)) continue;
                var entity = new ReferralCode { Id = Guid.NewGuid(), Code = code, CustomerId = customerId, Label = label, IsActive = true };
                _context.ReferralCodes.Add(entity);
                await _context.SaveChangesAsync();
                return (entity, null);
            }
            return (null, "A code could not be generated. Please try again.");
        }

        public Task<List<ReferralCode>> CodesAsync(Guid customerId) =>
            _context.ReferralCodes.AsNoTracking()
                .Where(c => c.CustomerId == customerId)
                .Include(c => c.Referrals)
                .OrderByDescending(c => c.IsActive).ThenByDescending(c => c.CreatedOn)
                .ToListAsync();

        public async Task<bool> SetActiveAsync(Guid codeId, Guid customerId, bool active)
        {
            var code = await _context.ReferralCodes.FirstOrDefaultAsync(c => c.Id == codeId && c.CustomerId == customerId);
            if (code is null) return false;
            if (active && !code.IsActive &&
                await _context.ReferralCodes.CountAsync(c => c.CustomerId == customerId && c.IsActive) >= MaxActiveCodes)
                return false;
            code.IsActive = active;
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>An active code and the customer who shares it (for the "invited by" note), or null.</summary>
        public async Task<ReferralCode?> FindActiveAsync(string? code)
        {
            var normalized = Normalize(code);
            if (normalized.Length == 0) return null;
            return await _context.ReferralCodes.AsNoTracking().Include(c => c.Customer)
                .FirstOrDefaultAsync(c => c.Code == normalized && c.IsActive);
        }

        /// <summary>
        /// Records that a newly registered account came with a code and tells the customer who shared it. Never throws
        /// for an unknown, switched-off or already used code: registration must not depend on it.
        /// </summary>
        public async Task RecordAsync(string? code, AppUser newAccount)
        {
            var referral = await FindActiveAsync(code);
            if (referral is null || string.IsNullOrEmpty(newAccount.Id)) return;
            if (newAccount.CustomerId == referral.CustomerId) return; // nobody refers themselves
            if (await _context.Referrals.AnyAsync(r => r.ReferredUserId == newAccount.Id)) return;

            _context.Referrals.Add(new Referral { Id = Guid.NewGuid(), ReferralCodeId = referral.Id, ReferredUserId = newAccount.Id });
            _customers.Add(referral.CustomerId, "A friend joined",
                $"{MaskEmail(newAccount.Email)} joined Michu Homes with your code {referral.Code}.",
                EntityAffecteds.AppUser, newAccount.Id, "/Portal/Referrals");
            await _context.SaveChangesAsync();
        }

        /// <summary>Friends who joined with any of the customer's codes, newest first, with how far they have got.</summary>
        public async Task<List<ReferredFriend>> FriendsAsync(Guid customerId)
        {
            var referrals = await _context.Referrals.AsNoTracking()
                .Where(r => r.ReferralCode!.CustomerId == customerId)
                .Select(r => new { r.ReferredUserId, r.CreatedOn, r.ReferralCode!.Code })
                .OrderByDescending(r => r.CreatedOn)
                .ToListAsync();
            if (referrals.Count == 0) return new();

            var userIds = referrals.Select(r => r.ReferredUserId).ToList();
            var accounts = await _context.AppUsers.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email, u.CustomerId })
                .ToDictionaryAsync(u => u.Id);
            var profileIds = accounts.Values.Where(a => a.CustomerId != null).Select(a => a.CustomerId!.Value).ToList();
            var profiles = await _context.UserProfiles.AsNoTracking()
                .Where(c => profileIds.Contains(c.Id))
                .Select(c => new { c.Id, c.FirstName, c.LastName })
                .ToDictionaryAsync(c => c.Id);
            var statuses = await _context.ApartmentHouseSaleTransactions.AsNoTracking()
                .Where(t => profileIds.Contains(t.CustomerId))
                .Select(t => new { t.CustomerId, t.Status })
                .ToListAsync();

            return referrals.Select(r =>
            {
                accounts.TryGetValue(r.ReferredUserId, out var account);
                var profileId = account?.CustomerId;
                var name = profileId is Guid id && profiles.TryGetValue(id, out var p) && !string.IsNullOrWhiteSpace(p.FirstName)
                    ? $"{p.FirstName.Trim()} {(string.IsNullOrWhiteSpace(p.LastName) ? string.Empty : p.LastName.Trim()[0] + ".")}".Trim()
                    : MaskEmail(account?.Email);
                var theirs = statuses.Where(s => s.CustomerId == profileId).Select(s => s.Status).ToList();
                var status = theirs.Contains(HouseReserveStatuses.Completed) ? "Bought a home"
                    : theirs.Any(s => ReservationService.Active.Contains(s)) ? "Reserved a home"
                    : profileId != null ? "Completed their profile"
                    : "Joined";
                return new ReferredFriend(name, r.CreatedOn, r.Code, status);
            }).ToList();
        }

        /// <summary>"abebe.k@example.com" → "ab•••@example.com".</summary>
        public static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "A new member";
            var at = email.IndexOf('@');
            if (at <= 0) return "A new member";
            var visible = email[..Math.Min(2, at)];
            return $"{visible}•••{email[at..]}";
        }

        [GeneratedRegex(@"^MH-[A-HJ-NP-Z2-9]{6}$")]
        private static partial Regex CodePattern();
    }
}
