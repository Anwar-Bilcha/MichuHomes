using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.Models; // Assuming your Notification model is here
using RadiantHomes.Services.NotificationService; // Assuming your INotificationService is here
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RadiantHomes.Pages.Admin.Notifications
{
    public class NotificationsAllModel : PageModel
    {
        private readonly INotificationService _notificationService;

        // Constructor to inject the notification service
        public NotificationsAllModel(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // Property to hold the list of all notifications
        public IList<Notification> Notifications { get; set; }

        // OnGetAsync method to fetch all notifications when the page is loaded
        public async Task OnGetAsync()
        {
            // Fetch all notifications.
            Notifications = await _notificationService.GetUnreadForAdminAsync();
        }

        // OnPostMarkAsReadAsync method to handle marking a notification as read
        public async Task<IActionResult> OnPostMarkAsReadAsync(int notificationId)
        {
            await _notificationService.MarkAsReadAsync(notificationId);

            try
            {
                await _notificationService.MarkAsReadAsync(notificationId);
                TempData["SuccessMessage"] = "Notification marked as read.";
            }
            catch (Exception ex)
            {

                ModelState.AddModelError(string.Empty, "Failed to mark notification as read. It might not exist.");

            }
                return RedirectToPage(); // Redirect back to the same page to refresh the list
            }

        
        // OnPostMarkAsUnreadAsync method to handle marking a notification as unread
        public async Task<IActionResult> OnPostMarkAsUnreadAsync(int notificationId)
        {
            try
            {
                await _notificationService.MarkAsReadAsync(notificationId);
                TempData["SuccessMessage"] = "Notification marked as read.";
            }
            catch (Exception ex)
            {

                ModelState.AddModelError(string.Empty, $"Failed to mark notification as read. It might not exist. {ex.Message}");

            }
            return RedirectToPage(); // Redirect back to the same page to refresh the list
        }


        // OnPostDeleteAsync method to handle deleting a notification
       
    }
}
