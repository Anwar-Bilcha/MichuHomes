using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Services.NotificationService;

namespace RadiantHomes.Services.CustomerPortal
{
    public enum ReservationOutcome { Reserved, ProfileRequired, NotFound, NotAvailable, CompanyNotReady, SaveFailed }

    public sealed record ReservationResult(ReservationOutcome Outcome, Guid? TransactionId = null)
    {
        public bool Succeeded => Outcome == ReservationOutcome.Reserved;

        public string Message => Outcome switch
        {
            ReservationOutcome.Reserved => "Reservation successful!",
            ReservationOutcome.ProfileRequired => "Could not find your customer profile.",
            ReservationOutcome.NotFound => "This house could not be found.",
            ReservationOutcome.NotAvailable => "This house is no longer available for reservation.",
            ReservationOutcome.CompanyNotReady => "This house can't be reserved online yet: its real estate company has no contact person.",
            _ => "The reservation could not be saved. Please try again.",
        };
    }

    public sealed record ReservationChange(bool Succeeded, string Message);

    /// <summary>
    /// Reservations of company apartments (ApartmentHouseSaleTransactions): a customer reserves (public apartment page
    /// and customer portal), may cancel while it is still pending, and the company approves it, marks it sold or
    /// cancels it (HousingCompanyAdmin/Reservations). Every step notifies the other side.
    /// </summary>
    /// <remarks>
    /// A reservation takes the apartment off the market (IsAvailable = false, Status "Reserved by Customer"); a sale
    /// keeps it off ("Sold"); a cancellation lists it again ("Approved"). Only listed apartments can be reserved.
    /// </remarks>
    public sealed class ReservationService
    {
        public static readonly HouseReserveStatuses[] Active = { HouseReserveStatuses.Pending, HouseReserveStatuses.Approved };

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly CustomerNotifications _customers;

        public ReservationService(ApplicationDbContext context, INotificationService notifications, CustomerNotifications customers)
        {
            _context = context;
            _notifications = notifications;
            _customers = customers;
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

            house.Status = "Reserved by Customer";
            house.IsAvailable = false;
            var transaction = new ApartmentHouseSaleTransaction
            {
                Id = Guid.NewGuid(), // the model's NEWID() default is not in the database
                ApartmentHouseId = house.Id,
                OwnerId = ownerId,
                CustomerId = buyerId,
                Notes = notes?.Trim() ?? string.Empty,
                TransactionDate = DateTime.Now,
                Status = HouseReserveStatuses.Pending,
            };
            _context.ApartmentHouseSaleTransactions.Add(transaction);
            _customers.Add(buyerId, "Reservation received",
                $"Your reservation of \"{house.Title}\" was sent to {company.Name}. They will contact you soon.",
                EntityAffecteds.ApartmentHouseSaleTransactions, transaction.Id.ToString(), "/Portal/Reservations");

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return new(ReservationOutcome.SaveFailed);
            }

            // The company's notification, as before (Admin/Notifications matches it to the reservation by house and time).
            await _notifications.CreateNotificationAsync(new Notification
            {
                Datatype = EntityAffecteds.ApartmentHouses.ToString(),
                ReceiverAdminId = ownerId,
                ReceiverCompanyId = company.CompanyId,
                RecipientRole = MichuHomesRoles.MichuAdmin,
                CreatedAt = DateTime.Now,
                DataKey = house.Id.ToString(),
                Message = $"House reservation request for {house.Id}",
                Title = $"{customerName} reserved a house",
                RedirectUrl = $"/ApartmentHouses/details?id={house.Id}",
            });
            return new(ReservationOutcome.Reserved, transaction.Id);
        }

        /// <summary>The customer's reservations (newest first) with the apartment, its site and company.</summary>
        public Task<List<ApartmentHouseSaleTransaction>> ForCustomerAsync(Guid customerId, params HouseReserveStatuses[] statuses) =>
            _context.ApartmentHouseSaleTransactions.AsNoTracking()
                .Where(t => t.CustomerId == customerId && statuses.Contains(t.Status))
                .Include(t => t.ApartmentHouse).ThenInclude(a => a.HouseAddress)
                .Include(t => t.ApartmentHouse).ThenInclude(a => a.Building!).ThenInclude(b => b.Project!).ThenInclude(p => p.Company)
                .Include(t => t.HomeOwner)
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
            ListAgain(transaction.ApartmentHouse);
            await _context.SaveChangesAsync();

            await _notifications.CreateNotificationAsync(new Notification
            {
                Datatype = EntityAffecteds.ApartmentHouseSaleTransactions.ToString(),
                ReceiverCompanyId = transaction.ApartmentHouse?.Building?.Project?.HousingCompanyId,
                RecipientRole = MichuHomesRoles.MichuAdmin,
                CreatedAt = DateTime.Now,
                DataKey = transaction.Id.ToString(),
                Title = "Reservation cancelled by the customer",
                Message = $"The customer cancelled the reservation of \"{transaction.ApartmentHouse?.Title}\".",
                RedirectUrl = "/HousingCompanyAdmin/Reservations",
            });
            return new(true, "Your reservation was cancelled.");
        }

        /// <summary>The company's decision (HousingCompanyAdmin/Reservations): approve, mark as sold or cancel.</summary>
        public async Task<ReservationChange> ChangeStatusAsync(Guid transactionId, HouseReserveStatuses to, CompanyScope scope)
        {
            var transaction = await WithApartment().FirstOrDefaultAsync(t => t.Id == transactionId);
            if (transaction is null || !scope.CanAccess(transaction.ApartmentHouse?.Building?.Project?.HousingCompanyId))
                return new(false, "This reservation could not be found.");

            var from = transaction.Status;
            var allowed = to switch
            {
                HouseReserveStatuses.Approved => from == HouseReserveStatuses.Pending,
                HouseReserveStatuses.Completed or HouseReserveStatuses.Cancelled => Active.Contains(from),
                _ => false,
            };
            if (!allowed) return new(false, "This reservation can no longer be changed that way.");

            var house = transaction.ApartmentHouse;
            var title = house?.Title ?? string.Empty;
            var company = house?.Building?.Project?.Company?.Name ?? "The real estate company";
            transaction.Status = to;
            string message;
            switch (to)
            {
                case HouseReserveStatuses.Approved:
                    _customers.Add(transaction.CustomerId, "Reservation approved",
                        $"{company} approved your reservation of \"{title}\".",
                        EntityAffecteds.ApartmentHouseSaleTransactions, transaction.Id.ToString(), "/Portal/Reservations");
                    message = $"Reservation of \"{title}\" approved.";
                    break;
                case HouseReserveStatuses.Completed:
                    if (house is not null)
                    {
                        house.Status = "Sold";
                        house.SalesStatus = "Sold";
                        house.IsAvailable = false;
                    }
                    _customers.Add(transaction.CustomerId, "Purchase completed",
                        $"Congratulations! Your purchase of \"{title}\" from {company} is complete.",
                        EntityAffecteds.ApartmentHouseSaleTransactions, transaction.Id.ToString(), "/Portal/Bought");
                    message = $"\"{title}\" was marked as sold.";
                    break;
                default:
                    ListAgain(house);
                    _customers.Add(transaction.CustomerId, "Reservation cancelled",
                        $"{company} cancelled your reservation of \"{title}\".",
                        EntityAffecteds.ApartmentHouseSaleTransactions, transaction.Id.ToString(), "/Portal/Reservations");
                    message = $"Reservation of \"{title}\" cancelled.";
                    break;
            }
            await _context.SaveChangesAsync();
            return new(true, message);
        }

        private IQueryable<ApartmentHouseSaleTransaction> WithApartment() =>
            _context.ApartmentHouseSaleTransactions
                .Include(t => t.ApartmentHouse).ThenInclude(a => a.Building!).ThenInclude(b => b.Project!).ThenInclude(p => p.Company);

        /// <summary>Back on the market after a cancellation (it was listed when it was reserved).</summary>
        private static void ListAgain(ApartmentHouse? house)
        {
            if (house is null) return;
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
