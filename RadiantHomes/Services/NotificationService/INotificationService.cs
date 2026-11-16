using System.Security.Claims;
using RadiantHomes.Models;
namespace RadiantHomes.Services.NotificationService
{
    public interface INotificationService
    {
        Task SendToAdminsAsync(string title, string message, string? redirectUrl = null);
        Task<List<Notification>> GetUnreadForAdminAsync();
        Task<Notification> CreateNotificationAsync(Notification notification);
        Task<List<Notification>> GetAllNotifications();
        Task<List<Notification>> GetAllNotifications(Guid companyId);
        Task<List<Notification>> GetUnreadForAdminAsync(Guid receiverAdminId);

        /// <summary>
        /// Every notification a Michu Admin (platform admin, no company) should see — read and unread,
        /// for every company and recipient role — newest first. Count the unread ones with <c>!n.IsRead</c>.
        /// This is the list the Admin/Notifications/NotificationsAll page shows to Michu Admins.
        /// </summary>
        Task<List<Notification>> GetForMichuAdminAsync();

        /// <summary>Marks the notification as read. Returns false when it doesn't exist.</summary>
        Task<bool> MarkAsReadAsync(int notificationId);

        /// <summary>Marks the notification as unread. Returns false when it doesn't exist.</summary>
        Task<bool> MarkAsUnreadAsync(int notificationId);

        /// <summary>Deletes the notification. Returns false when it doesn't exist.</summary>
        Task<bool> DeleteAsync(int notificationId);

        /// <summary>
        /// The approval state of what each notification is about, read from that record: the houses listed in a
        /// "houses pending approval" notification (their Status), the reservation behind "X reserved a house"
        /// (its ApartmentHouseSaleTransaction status) or the profile behind a profile notification (ProfileStatus).
        /// Notifications without an approval concept get <see cref="NotificationApproval.NotApplicable"/>.
        /// Runs at most one query per kind of record, however many notifications are passed. Keyed by notification id.
        /// </summary>
        Task<IReadOnlyDictionary<int, NotificationApproval>> GetApprovalStatusesAsync(IEnumerable<Notification> notifications);

        /// <summary>
        /// Approves the houses listed in a "houses pending approval" notification and marks it read. Houses that are
        /// already approved, reserved, sold, rented or hidden are left unchanged; the result says which status values
        /// were replaced and which houses were skipped.
        /// </summary>
        Task<HouseApprovalResult> ApproveHousesAsync(int notificationId);

        /// <summary>
        /// Whether the user may see and act on the notification: a Michu Admin any notification, a Real Estate Admin
        /// only notifications addressed to their own company (ReceiverCompanyId), anyone else none.
        /// </summary>
        Task<bool> CanAccessAsync(ClaimsPrincipal user, Notification notification);

        /// <summary>Only a Michu Admin may approve houses (approval is a platform decision).</summary>
        bool CanApproveHouses(ClaimsPrincipal user);

        /// <summary>The user's housing company: AppUser.HousingCompanyId, falling back to their Customer profile's CompanyId.</summary>
        Task<Guid?> GetUserCompanyIdAsync(ClaimsPrincipal user);
    }

}
