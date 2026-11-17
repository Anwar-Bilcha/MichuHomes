using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Services.NotificationService;
using RadiantHomes.Services.Settings;

namespace RadiantHomes.Services.CustomerPortal
{
    public enum ReservationOutcome { Reserved, ProfileRequired, NotFound, NotAvailable, CompanyNotReady, SaveFailed }

    public sealed record ReservationResult(ReservationOutcome Outcome, Guid? TransactionId = null, string? Reference = null)
    {
        public bool Succeeded => Outcome == ReservationOutcome.Reserved;

        public string Message => Outcome switch
        {
            ReservationOutcome.Reserved => $"Reservation successful! Your reference is {Reference}.",
            ReservationOutcome.ProfileRequired => "Could not find your customer profile.",
            ReservationOutcome.NotFound => "This house could not be found.",
            ReservationOutcome.NotAvailable => "This house is no longer available for reservation.",
            ReservationOutcome.CompanyNotReady => "This house can't be reserved online yet: its real estate company has no contact person.",
            _ => "The reservation could not be saved. Please try again.",
        };
    }

    public sealed record ReservationChange(bool Succeeded, string Message);

    /// <summary>
    /// Reservations of company apartments (ApartmentHouseSaleTransactions). A customer reserves (public apartment page
    /// and portal) and may cancel while it is pending; the company approves, rejects (with a reason), cancels an approved
    /// one (with a reason) or marks it sold (HousingCompanyAdmin/Reservations); a pending reservation the company doesn't
    /// answer in time expires (ReservationExpiryJob, Admin/Settings). Every change is in ReservationStatusChanges and
    /// notifies the other side.
    /// </summary>
    /// <remarks>
    /// Integrity: the database allows only one active (pending/approved) reservation and one sale per apartment
    /// (filtered unique indexes), so simultaneous requests can't both succeed; each reservation has a RowVersion, so two
    /// people changing the same reservation at once can't both win. A home is listed again only when nothing else holds
    /// it and it isn't sold or hidden.
    /// </remarks>
    public sealed class ReservationService
    {
        public static readonly HouseReserveStatuses[] Active = { HouseReserveStatuses.Pending, HouseReserveStatuses.Approved };
        public const string ReservedStatus = "Reserved by Customer";
        private const string ReferenceAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly CustomerNotifications _customers;
        private readonly PlatformSettings _settings;

        public ReservationService(ApplicationDbContext context, INotificationService notifications, CustomerNotifications customers, PlatformSettings settings)
        {
            _context = context;
            _notifications = notifications;
            _customers = customers;
            _settings = settings;
        }

        public static string Label(HouseReserveStatuses status) => status switch
        {
            HouseReserveStatuses.Pending => "Waiting",
            HouseReserveStatuses.Approved => "Approved",
            HouseReserveStatuses.Completed => "Sold",
            HouseReserveStatuses.Cancelled => "Cancelled",
            HouseReserveStatuses.Expired => "Expired",
            HouseReserveStatuses.Rejected => "Declined",
            _ => status.ToString(),
        };

        public static string Badge(HouseReserveStatuses status) => status switch
        {
            HouseReserveStatuses.Pending => "text-bg-warning",
            HouseReserveStatuses.Approved => "text-bg-info",
            HouseReserveStatuses.Completed => "text-bg-success",
            HouseReserveStatuses.Rejected => "text-bg-danger",
            _ => "text-bg-secondary",
        };

        public static string NewReference()
        {
            Span<char> code = stackalloc char[5];
            for (var i = 0; i < code.Length; i++) code[i] = ReferenceAlphabet[RandomNumberGenerator.GetInt32(ReferenceAlphabet.Length)];
            return $"R-{DateTime.UtcNow:yyMM}-{code}";
        }

        public async Task<ReservationResult> ReserveAsync(Guid apartmentId, Guid? customerId, string? customerName, string? notes)
        {
            if (customerId is not Guid buyerId || buyerId == Guid.Empty) return new(ReservationOutcome.ProfileRequired);

            var house = await _context.ApartmentHouses
                .Include(a => a.Building!).ThenInclude(b => b.Project!).ThenInclude(p => p.Company!).ThenInclude(c => c.ContactPerson)
                .FirstOrDefaultAsync(a => a.Id == apartmentId);
            if (house is null) return new(ReservationOutcome.NotFound);

            var company = house.Building?.Project?.Company;
            var ownerId = company?.ContactPerson.FirstOrDefault()?.Id ?? Guid.Empty;
            if (company is null || ownerId == Guid.Empty) return new(ReservationOutcome.CompanyNotReady);
            // Only approved companies sell (pending, rejected and suspended companies are hidden from customers).
            if (company.Status != RadiantHomes.Models.Security.CompanyStatuses.Approved) return new(ReservationOutcome.NotAvailable);
            // Listed apartments only (approved and available), the same rule as the public listing.
            if (!house.IsAvailable || !string.Equals(house.Status, ApartmentHouseApproval.ApprovedStatus, StringComparison.OrdinalIgnoreCase))
                return new(ReservationOutcome.NotAvailable);

            var hours = await _settings.GetIntAsync(SettingKeys.ReservationPendingExpiryHours);
            house.Status = ReservedStatus;
            house.IsAvailable = false;
            var transaction = new ApartmentHouseSaleTransaction
            {
                Id = Guid.NewGuid(),
                ApartmentHouseId = house.Id,
                OwnerId = ownerId,
                CustomerId = buyerId,
                CompanyId = company.CompanyId,
                Notes = notes?.Trim() ?? string.Empty,
                TransactionDate = DateTime.Now,
                Status = HouseReserveStatuses.Pending,
                Reference = NewReference(),
                ExpiresAt = hours > 0 ? DateTime.UtcNow.AddHours(hours) : null,
            };
            _context.ApartmentHouseSaleTransactions.Add(transaction);
            AddHistory(transaction, null, HouseReserveStatuses.Pending, null, null, customerName);
            _customers.Add(buyerId, "Reservation received",
                $"Your reservation {transaction.Reference} of \"{house.Title}\" was sent to {company.Name}."
                + (transaction.ExpiresAt is DateTime exp ? $" If they don't answer by {exp.ToLocalTime():d MMM HH:mm}, it expires and the home is listed again." : ""),
                EntityAffecteds.ApartmentHouseSaleTransactions, transaction.Id.ToString(), "/Portal/Reservations");

            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    break;
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex, "Reference") && attempt < 3)
                {
                    transaction.Reference = NewReference(); // a reference that is already taken (very rare): draw another
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex, "OneActivePerApartment"))
                {
                    // Someone else reserved it a moment earlier: the database refused the second reservation.
                    DiscardChanges();
                    return new(ReservationOutcome.NotAvailable);
                }
                catch (DbUpdateException)
                {
                    DiscardChanges();
                    return new(ReservationOutcome.SaveFailed);
                }
            }

            await _notifications.CreateNotificationAsync(new Notification
            {
                Datatype = EntityAffecteds.ApartmentHouses.ToString(),
                ReceiverAdminId = ownerId,
                ReceiverCompanyId = company.CompanyId,
                RecipientRole = MichuHomesRoles.MichuAdmin,
                CreatedAt = DateTime.Now,
                DataKey = house.Id.ToString(),
                Message = $"Reservation {transaction.Reference} of \"{house.Title}\"" + (transaction.ExpiresAt is DateTime e ? $" — answer by {e.ToLocalTime():d MMM HH:mm}." : "."),
                Title = $"{customerName} reserved a house",
                RedirectUrl = "/HousingCompanyAdmin/Reservations",
            });
            return new(ReservationOutcome.Reserved, transaction.Id, transaction.Reference);
        }

        /// <summary>The customer's reservations (newest first) with the apartment, its site and company.</summary>
        public Task<List<ApartmentHouseSaleTransaction>> ForCustomerAsync(Guid customerId, params HouseReserveStatuses[] statuses) =>
            _context.ApartmentHouseSaleTransactions.AsNoTracking()
                .Where(t => t.CustomerId == customerId && statuses.Contains(t.Status))
                .Include(t => t.ApartmentHouse).ThenInclude(a => a.HouseAddress)
                .Include(t => t.ApartmentHouse).ThenInclude(a => a.Building!).ThenInclude(b => b.Project!).ThenInclude(p => p.Company)
                .Include(t => t.HomeOwner)
                .Include(t => t.History)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

        /// <summary>The customer's own open (or completed) reservation of this apartment, if any.</summary>
        public Task<ApartmentHouseSaleTransaction?> ActiveForAsync(Guid customerId, Guid apartmentId) =>
            _context.ApartmentHouseSaleTransactions.AsNoTracking()
                .Where(t => t.CustomerId == customerId && t.ApartmentHouseId == apartmentId)
                .Where(t => t.Status == HouseReserveStatuses.Pending || t.Status == HouseReserveStatuses.Approved || t.Status == HouseReserveStatuses.Completed)
                .OrderByDescending(t => t.TransactionDate)
                .FirstOrDefaultAsync();

        public async Task<ReservationChange> CancelByCustomerAsync(Guid transactionId, Guid customerId)
        {
            var transaction = await WithApartment().FirstOrDefaultAsync(t => t.Id == transactionId && t.CustomerId == customerId);
            if (transaction is null) return new(false, "This reservation could not be found.");
            if (transaction.Status != HouseReserveStatuses.Pending)
                return new(false, "Only reservations that are still waiting can be cancelled here. Please contact the real estate company.");

            transaction.Status = HouseReserveStatuses.Cancelled;
            AddHistory(transaction, HouseReserveStatuses.Pending, HouseReserveStatuses.Cancelled, "Cancelled by the customer.", null, null);
            await ListAgainAsync(transaction.ApartmentHouse, transaction.Id);
            if (!await TrySaveAsync()) return new(false, Conflict);

            await _notifications.CreateNotificationAsync(new Notification
            {
                Datatype = EntityAffecteds.ApartmentHouseSaleTransactions.ToString(),
                ReceiverCompanyId = transaction.CompanyId ?? transaction.ApartmentHouse?.Building?.Project?.HousingCompanyId,
                RecipientRole = MichuHomesRoles.MichuAdmin,
                CreatedAt = DateTime.Now,
                DataKey = transaction.Id.ToString(),
                Title = "Reservation cancelled by the customer",
                Message = $"The customer cancelled reservation {transaction.Reference} of \"{transaction.ApartmentHouse?.Title}\".",
                RedirectUrl = "/HousingCompanyAdmin/Reservations",
            });
            return new(true, "Your reservation was cancelled.");
        }

        /// <summary>
        /// The company's decision (HousingCompanyAdmin/Reservations): approve or decline a pending reservation, cancel an
        /// approved one, or mark it sold. Declining and cancelling need a reason, which the customer sees.
        /// </summary>
        public async Task<ReservationChange> ChangeStatusAsync(Guid transactionId, HouseReserveStatuses to, CompanyScope scope,
            string? reason = null, string? actorUserId = null, string? actorName = null)
        {
            var transaction = await WithApartment().FirstOrDefaultAsync(t => t.Id == transactionId);
            if (transaction is null || !scope.CanAccess(transaction.ApartmentHouse?.Building?.Project?.HousingCompanyId))
                return new(false, "This reservation could not be found.");

            var from = transaction.Status;
            var allowed = to switch
            {
                HouseReserveStatuses.Approved => from == HouseReserveStatuses.Pending,
                HouseReserveStatuses.Rejected => from == HouseReserveStatuses.Pending,
                HouseReserveStatuses.Cancelled => from == HouseReserveStatuses.Approved,
                HouseReserveStatuses.Completed => Active.Contains(from),
                _ => false,
            };
            if (!allowed) return new(false, "This reservation can no longer be changed that way.");
            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (to is HouseReserveStatuses.Rejected or HouseReserveStatuses.Cancelled && reason is null)
                return new(false, "Please give the customer a reason.");
            if (reason is { Length: > 1000 }) return new(false, "Keep the reason under 1000 characters.");

            var house = transaction.ApartmentHouse;
            var title = house?.Title ?? string.Empty;
            var company = house?.Building?.Project?.Company?.Name ?? "The real estate company";
            transaction.Status = to;
            if (to != HouseReserveStatuses.Pending) transaction.ExpiresAt = null;
            AddHistory(transaction, from, to, reason, actorUserId, actorName);
            string message;
            switch (to)
            {
                case HouseReserveStatuses.Approved:
                    Notify(transaction, "Reservation approved", $"{company} approved your reservation {transaction.Reference} of \"{title}\".", "/Portal/Reservations");
                    message = $"Reservation {transaction.Reference} approved.";
                    break;
                case HouseReserveStatuses.Completed:
                    if (house is not null)
                    {
                        house.Status = "Sold";
                        house.SalesStatus = "Sold";
                        house.IsAvailable = false;
                    }
                    Notify(transaction, "Purchase completed", $"Congratulations! Your purchase of \"{title}\" from {company} is complete (reservation {transaction.Reference}).", "/Portal/Bought");
                    message = $"\"{title}\" was marked as sold.";
                    break;
                case HouseReserveStatuses.Rejected:
                    await ListAgainAsync(house, transaction.Id);
                    Notify(transaction, "Reservation declined", $"{company} declined your reservation {transaction.Reference} of \"{title}\": {reason}", "/Portal/Reservations");
                    message = $"Reservation {transaction.Reference} declined.";
                    break;
                default:
                    await ListAgainAsync(house, transaction.Id);
                    Notify(transaction, "Reservation cancelled", $"{company} cancelled your reservation {transaction.Reference} of \"{title}\": {reason}", "/Portal/Reservations");
                    message = $"Reservation {transaction.Reference} cancelled.";
                    break;
            }
            try
            {
                if (!await TrySaveAsync()) return new(false, Conflict);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex, "OneSalePerApartment"))
            {
                DiscardChanges();
                return new(false, "This home was already sold through another reservation.");
            }
            return new(true, message);
        }

        /// <summary>
        /// Pending reservations past their answer time become Expired: the home is listed again and both sides are told.
        /// Returns how many expired. Called by ReservationExpiryJob.
        /// </summary>
        public async Task<int> ExpireDueAsync(DateTime? nowUtc = null, int batchSize = 200)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            var due = await WithApartment()
                .Where(t => t.Status == HouseReserveStatuses.Pending && t.ExpiresAt != null && t.ExpiresAt <= now)
                .OrderBy(t => t.ExpiresAt).Take(batchSize).ToListAsync();
            var expired = 0;
            foreach (var transaction in due)
            {
                transaction.Status = HouseReserveStatuses.Expired;
                transaction.ExpiresAt = null;
                AddHistory(transaction, HouseReserveStatuses.Pending, HouseReserveStatuses.Expired, "The company didn't answer in time.", null, "System");
                await ListAgainAsync(transaction.ApartmentHouse, transaction.Id);
                var title = transaction.ApartmentHouse?.Title ?? string.Empty;
                Notify(transaction, "Reservation expired",
                    $"Your reservation {transaction.Reference} of \"{title}\" expired because the company didn't answer in time. The home is listed again; you can reserve it again.",
                    "/Portal/Reservations");
                _context.Notifications.Add(new Notification
                {
                    Datatype = EntityAffecteds.ApartmentHouseSaleTransactions.ToString(),
                    ReceiverCompanyId = transaction.CompanyId ?? transaction.ApartmentHouse?.Building?.Project?.HousingCompanyId,
                    RecipientRole = MichuHomesRoles.MichuAdmin,
                    CreatedAt = DateTime.Now,
                    DataKey = transaction.Id.ToString(),
                    Title = "Reservation expired",
                    Message = $"Reservation {transaction.Reference} of \"{title}\" expired without an answer; the home is listed again.",
                    RedirectUrl = "/HousingCompanyAdmin/Reservations?status=all",
                });
                if (await TrySaveAsync()) expired++; // changed by someone meanwhile: skipped, picked up next time if still due
            }
            return expired;
        }

        public Task<List<ReservationStatusChange>> HistoryAsync(Guid transactionId) =>
            _context.ReservationStatusChanges.AsNoTracking().Where(h => h.TransactionId == transactionId).OrderBy(h => h.ChangedOn).ThenBy(h => h.Id).ToListAsync();

        private const string Conflict = "Someone changed this reservation at the same moment. Reload the page and try again.";

        private void AddHistory(ApartmentHouseSaleTransaction transaction, HouseReserveStatuses? from, HouseReserveStatuses to, string? reason, string? actorUserId, string? actorName) =>
            _context.ReservationStatusChanges.Add(new ReservationStatusChange
            {
                TransactionId = transaction.Id, FromStatus = from, ToStatus = to, Reason = reason,
                ChangedByUserId = actorUserId, ChangedByName = actorName, ChangedOn = DateTime.UtcNow,
            });

        private void Notify(ApartmentHouseSaleTransaction transaction, string title, string message, string url) =>
            _customers.Add(transaction.CustomerId, title, message, EntityAffecteds.ApartmentHouseSaleTransactions, transaction.Id.ToString(), url);

        private async Task<bool> TrySaveAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                DiscardChanges();
                return false;
            }
        }

        private void DiscardChanges()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList())
            {
                if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
                else if (entry.State is EntityState.Modified or EntityState.Deleted) entry.Reload();
            }
        }

        private static bool IsUniqueViolation(DbUpdateException ex, string indexNamePart) =>
            ex.InnerException is SqlException sql && sql.Number is 2601 or 2627 && sql.Message.Contains(indexNamePart, StringComparison.OrdinalIgnoreCase);

        private IQueryable<ApartmentHouseSaleTransaction> WithApartment() =>
            _context.ApartmentHouseSaleTransactions
                .Include(t => t.ApartmentHouse).ThenInclude(a => a.Building!).ThenInclude(b => b.Project!).ThenInclude(p => p.Company);

        /// <summary>
        /// Back on the market after a cancellation, decline or expiry: only when the home is still held by this reservation
        /// ("Reserved by Customer"), isn't sold, and no other reservation holds it. A home an admin hid stays hidden.
        /// </summary>
        private async Task ListAgainAsync(ApartmentHouse? house, Guid releasedTransactionId)
        {
            if (house is null) return;
            if (!string.Equals(house.Status, ReservedStatus, StringComparison.OrdinalIgnoreCase)) return;
            if (string.Equals(house.SalesStatus, "Sold", StringComparison.OrdinalIgnoreCase)) return;
            var heldElsewhere = await _context.ApartmentHouseSaleTransactions.AnyAsync(t => t.ApartmentHouseId == house.Id && t.Id != releasedTransactionId
                && (t.Status == HouseReserveStatuses.Pending || t.Status == HouseReserveStatuses.Approved || t.Status == HouseReserveStatuses.Completed));
            if (heldElsewhere) return;
            house.IsAvailable = true;
            house.Status = ApartmentHouseApproval.ApprovedStatus;
            house.SalesStatus = "None";
        }
    }

    /// <summary>Customers' comments (with a 1–5 rating) on company apartments (ApartmentHouseReviews).</summary>
    public sealed class ApartmentComments
    {
        public const int MaxLength = 1000;
        private readonly ApplicationDbContext _context;

        public ApartmentComments(ApplicationDbContext context) => _context = context;

        /// <summary>Returns an error message, or null when the comment was saved.</summary>
        public async Task<string?> AddAsync(Guid apartmentId, Guid customerId, int rating, string? comment)
        {
            if (!await _context.ApartmentHouses.AnyAsync(a => a.Id == apartmentId)) return "This house could not be found.";
            comment = comment?.Trim();
            if (rating < 1 || rating > 5 || string.IsNullOrEmpty(comment)) return "Please write a comment and choose a rating from 1 to 5.";
            if (comment.Length > MaxLength) return $"Comments can be at most {MaxLength} characters.";

            _context.ApartmentHouseReviews.Add(new ApartmentHouseReview
            {
                Id = Guid.NewGuid(),
                HouseId = apartmentId,
                ReviewerCustomerId = customerId,
                Rating = rating,
                Comment = comment,
                ReviewDate = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();
            return null;
        }

        public Task<List<ApartmentHouseReview>> ForApartmentAsync(Guid apartmentId) =>
            _context.ApartmentHouseReviews.AsNoTracking()
                .Where(r => r.HouseId == apartmentId)
                .Include(r => r.ReviewerCustomer)
                .OrderByDescending(r => r.ReviewDate)
                .ToListAsync();

        public Task<List<ApartmentHouseReview>> ByCustomerAsync(Guid customerId) =>
            _context.ApartmentHouseReviews.AsNoTracking()
                .Where(r => r.ReviewerCustomerId == customerId)
                .Include(r => r.ApartmentHouse)
                .OrderByDescending(r => r.ReviewDate)
                .ToListAsync();
    }
}
