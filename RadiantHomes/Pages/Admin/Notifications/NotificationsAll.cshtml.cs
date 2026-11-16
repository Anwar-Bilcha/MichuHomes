using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Services.NotificationService;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RadiantHomes.Pages.Admin.Notifications
{
    public class NotificationsAllModel : PageModel
    {
        private const string NotFoundMessage = "That notification doesn't exist or you don't have access to it.";

        private readonly INotificationService _notificationService;
        private readonly ApplicationDbContext _context;

        public NotificationsAllModel(INotificationService notificationService, ApplicationDbContext context)
        {
            _notificationService = notificationService;
            _context = context;
        }

        // Read and unread notifications, newest first
        public IList<Notification> Notifications { get; set; } = new List<Notification>();

        // Approval state of what each notification is about (houses, reservation, profile), by notification id
        public IReadOnlyDictionary<int, NotificationApproval> Approvals { get; set; } = new Dictionary<int, NotificationApproval>();

        public async Task OnGetAsync()
        {
            if (User.IsInRole(MichuHomesRoles.MichuAdmin))
            {
                // Michu Admins see every notification (all companies, read and unread)
                Notifications = await _notificationService.GetForMichuAdminAsync();
            }
            else
            {
                // Company admins see their own company's notifications (same source as the layout's bell)
                var companyId = await _notificationService.GetUserCompanyIdAsync(User);
                Notifications = companyId == null
                    ? new List<Notification>()
                    : (await _notificationService.GetAllNotifications(companyId.Value)).OrderByDescending(n => n.CreatedAt).ToList();
            }

            Approvals = await _notificationService.GetApprovalStatusesAsync(Notifications);
        }

        public NotificationApproval ApprovalFor(Notification notification) =>
            Approvals.TryGetValue(notification.Id, out var approval) ? approval : NotificationApproval.NotApplicable;

        public async Task<IActionResult> OnPostMarkAsReadAsync(int notificationId)
        {
            if (!await CanAccessAsync(notificationId)) return RedirectWithError(NotFoundMessage);

            if (await _notificationService.MarkAsReadAsync(notificationId))
                TempData["SuccessMessage"] = "Notification marked as read.";
            else
                TempData["ErrorMessage"] = "Failed to mark the notification as read. It might not exist.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMarkAsUnreadAsync(int notificationId)
        {
            if (!await CanAccessAsync(notificationId)) return RedirectWithError(NotFoundMessage);

            if (await _notificationService.MarkAsUnreadAsync(notificationId))
                TempData["SuccessMessage"] = "Notification marked as unread.";
            else
                TempData["ErrorMessage"] = "Failed to mark the notification as unread. It might not exist.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int notificationId)
        {
            if (!await CanAccessAsync(notificationId)) return RedirectWithError(NotFoundMessage);

            if (await _notificationService.DeleteAsync(notificationId))
                TempData["SuccessMessage"] = "Notification deleted.";
            else
                TempData["ErrorMessage"] = "Failed to delete the notification. It might not exist.";

            return RedirectToPage();
        }

        // Michu Admins: any notification; Real Estate Admins: only their own company's
        private async Task<bool> CanAccessAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            return notification != null && await _notificationService.CanAccessAsync(User, notification);
        }

        private IActionResult RedirectWithError(string message)
        {
            TempData["ErrorMessage"] = message;
            return RedirectToPage();
        }
    }
}
