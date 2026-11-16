using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Services.NotificationService
{
    public class NotificationService : INotificationService
    {
        // A reservation notification and its sale transaction are saved in the same request (seconds apart at most).
        private static readonly TimeSpan ReservationMatchWindow = TimeSpan.FromMinutes(5);

        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> CanAccessAsync(ClaimsPrincipal user, Notification notification)
        {
            if (user.IsInRole(MichuHomesRoles.MichuAdmin)) return true;
            if (!user.IsInRole(MichuHomesRoles.RealEstateAdmin)) return false;

            var companyId = await GetUserCompanyIdAsync(user);
            return companyId != null && notification.ReceiverCompanyId == companyId;
        }

        public bool CanApproveHouses(ClaimsPrincipal user) => user.IsInRole(MichuHomesRoles.MichuAdmin);

        // One definition of "the user's company" for the whole app (tenant scoping): see CompanyScope
        public async Task<Guid?> GetUserCompanyIdAsync(ClaimsPrincipal user) =>
            (await CompanyScope.ForAsync(user, _context)).CompanyId;
        async Task<Notification> INotificationService.CreateNotificationAsync(Notification notification)
        {
            try
            {
                var resultFromDb = _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                return resultFromDb.Entity;
            }
            catch (Exception ex)
            {
                Notification result = new Notification();
                result.Message = ex.Message;
                return result;
            }
        }
        public async Task SendToAdminsAsync(string title, string message, string? redirectUrl = null)
        {
            var notification = new Notification
            {
                Title = title,
                Message = message,
                RedirectUrl = redirectUrl,
                RecipientRole = "Admin",
                CreatedAt = DateTime.Now,
                IsRead = false,
            };


            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetUnreadForAdminAsync()
        {
            return await _context.Notifications
                .Where(n => !n.IsRead && n.RecipientRole != CustomerRecipientRole)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        // Every notification except the customers' own (customer portal; see CustomerPortal/CustomerNotifications).
        public async Task<List<Notification>> GetForMichuAdminAsync()
        {
            return await _context.Notifications
                .Where(n => n.RecipientRole != CustomerRecipientRole)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        private const string CustomerRecipientRole = MichuHomesRoles.Buyer;

        public Task<bool> MarkAsReadAsync(int notificationId) => SetReadAsync(notificationId, true);

        public Task<bool> MarkAsUnreadAsync(int notificationId) => SetReadAsync(notificationId, false);

        public async Task<bool> DeleteAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification == null)
            {
                return false;
            }

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> SetReadAsync(int notificationId, bool isRead)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification == null)
            {
                return false;
            }

            if (notification.IsRead != isRead)
            {
                notification.IsRead = isRead;
                await _context.SaveChangesAsync();
            }
            return true;
        }

        public async Task<IReadOnlyDictionary<int, NotificationApproval>> GetApprovalStatusesAsync(IEnumerable<Notification> notifications)
        {
            var items = notifications
                .Select(n => (Notification: n, Subject: NotificationSubjects.Of(n), Ids: NotificationSubjects.ParseIds(n.DataKey)))
                .ToList();
            var result = items.ToDictionary(i => i.Notification.Id, _ => NotificationApproval.NotApplicable);

            // Houses awaiting approval: one query for every house of every such notification
            var approvalHouseIds = items.Where(i => i.Subject == NotificationSubject.HouseApproval)
                .SelectMany(i => i.Ids).Distinct().ToList();
            if (approvalHouseIds.Count > 0)
            {
                var statusById = await _context.ApartmentHouses
                    .Where(h => approvalHouseIds.Contains(h.Id))
                    .Select(h => new { h.Id, h.Status })
                    .ToDictionaryAsync(h => h.Id, h => h.Status);

                foreach (var item in items.Where(i => i.Subject == NotificationSubject.HouseApproval))
                {
                    var statuses = item.Ids.Where(statusById.ContainsKey).Select(id => statusById[id]).ToList();
                    if (statuses.Count == 0) continue; // the houses no longer exist
                    var approved = statuses.Count(ApartmentHouseApproval.IsApproved);
                    result[item.Notification.Id] =
                        approved == statuses.Count ? new NotificationApproval(NotificationApprovalState.Approved, "Approved")
                        : approved == 0 ? new NotificationApproval(NotificationApprovalState.Pending, "Pending")
                        : new NotificationApproval(NotificationApprovalState.PartlyApproved, $"{approved} of {statuses.Count} approved");
                }
            }

            // Reservations: the sale transaction saved together with the notification
            var reservedHouseIds = items.Where(i => i.Subject == NotificationSubject.HouseReservation)
                .Select(i => i.Ids[0]).Distinct().ToList();
            if (reservedHouseIds.Count > 0)
            {
                var transactions = (await _context.ApartmentHouseSaleTransactions
                        .Where(t => reservedHouseIds.Contains(t.ApartmentHouseId))
                        .Select(t => new { t.ApartmentHouseId, t.TransactionDate, t.Status })
                        .ToListAsync())
                    .ToLookup(t => t.ApartmentHouseId);

                foreach (var item in items.Where(i => i.Subject == NotificationSubject.HouseReservation))
                {
                    var createdAt = item.Notification.CreatedAt;
                    var match = transactions[item.Ids[0]]
                        .Where(t => (t.TransactionDate - createdAt).Duration() <= ReservationMatchWindow)
                        .OrderBy(t => (t.TransactionDate - createdAt).Duration())
                        .FirstOrDefault();
                    if (match == null) continue;
                    result[item.Notification.Id] = match.Status switch
                    {
                        HouseReserveStatuses.Pending => new NotificationApproval(NotificationApprovalState.Pending, "Pending"),
                        HouseReserveStatuses.Approved => new NotificationApproval(NotificationApprovalState.Approved, "Approved"),
                        HouseReserveStatuses.Completed => new NotificationApproval(NotificationApprovalState.Completed, "Completed"),
                        HouseReserveStatuses.Cancelled => new NotificationApproval(NotificationApprovalState.Cancelled, "Cancelled"),
                        HouseReserveStatuses.Closed => new NotificationApproval(NotificationApprovalState.Closed, "Closed"),
                        _ => new NotificationApproval(NotificationApprovalState.Pending, match.Status.ToString()),
                    };
                }
            }

            // Profiles awaiting approval: the profile's own status
            var profileIds = items.Where(i => i.Subject == NotificationSubject.ProfileApproval)
                .Select(i => i.Ids[0]).Distinct().ToList();
            if (profileIds.Count > 0)
            {
                var profileStatusById = await _context.UserProfiles
                    .Where(p => profileIds.Contains(p.Id))
                    .Select(p => new { p.Id, p.ProfileStatus })
                    .ToDictionaryAsync(p => p.Id, p => p.ProfileStatus);

                foreach (var item in items.Where(i => i.Subject == NotificationSubject.ProfileApproval))
                {
                    if (!profileStatusById.TryGetValue(item.Ids[0], out var status)) continue;
                    var value = status?.Trim() ?? string.Empty;
                    result[item.Notification.Id] = value.ToLowerInvariant() switch
                    {
                        "approved" => new NotificationApproval(NotificationApprovalState.Approved, "Approved"),
                        "pending" or "" => new NotificationApproval(NotificationApprovalState.Pending, "Pending"),
                        "rejected" => new NotificationApproval(NotificationApprovalState.Rejected, "Rejected"),
                        _ => new NotificationApproval(NotificationApprovalState.Closed, value),
                    };
                }
            }

            return result;
        }

        public async Task<HouseApprovalResult> ApproveHousesAsync(int notificationId)
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId);
            if (notification == null)
                return new HouseApprovalResult { NotificationFound = false, IsHouseApproval = false };
            if (NotificationSubjects.Of(notification) != NotificationSubject.HouseApproval)
                return new HouseApprovalResult { IsHouseApproval = false };

            var houseIds = NotificationSubjects.ParseIds(notification.DataKey);
            var houses = await _context.ApartmentHouses.Where(h => houseIds.Contains(h.Id)).ToListAsync();
            var result = new HouseApprovalResult { HousesFound = houses.Count };

            foreach (var house in houses)
            {
                var previous = house.Status?.Trim() ?? string.Empty;
                if (ApartmentHouseApproval.IsApproved(previous))
                {
                    result.AlreadyApproved++;
                    continue;
                }
                if (!ApartmentHouseApproval.CanApprove(previous))
                {
                    result.Skipped[previous] = result.Skipped.GetValueOrDefault(previous) + 1;
                    continue;
                }

                house.Status = ApartmentHouseApproval.ApprovedStatus; // UpdatedOn/UpdatedBy are stamped by SaveChanges
                result.Approved++;
                if (ApartmentHouseApproval.ApprovingLosesInformation(previous))
                {
                    result.Replaced[previous] = result.Replaced.GetValueOrDefault(previous) + 1;
                    // Status was the only place this value was kept: leave a trace of it in the log.
                    _logger.LogInformation(
                        "Notification {NotificationId}: apartment house {HouseId} approved; its previous Status \"{PreviousStatus}\" was replaced by \"{ApprovedStatus}\".",
                        notification.Id, house.Id, previous, ApartmentHouseApproval.ApprovedStatus);
                }
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();
            return result;
        }

        async Task<List<Notification>> INotificationService.GetAllNotifications()
        {
            return await _context.Notifications.ToListAsync();
        }

        async Task<List<Notification>> INotificationService.GetAllNotifications(Guid companyId)
        {
            return await _context.Notifications.Where(a=>a.ReceiverCompanyId==companyId).ToListAsync();
        }

        async Task<List<Notification>> INotificationService.GetUnreadForAdminAsync(Guid companyId)
        {
            return await _context.Notifications
                .Where(a => (a.ReceiverCompanyId == companyId || a.ReceiverAdminId == companyId) && !a.IsRead)
                .ToListAsync();
        }
    }

}
