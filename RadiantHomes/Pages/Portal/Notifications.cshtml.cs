using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.Models;
using RadiantHomes.Services.CustomerPortal;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>The customer's notifications (reservations, purchases, friends who joined).</summary>
    public class NotificationsModel : PortalPageModel
    {
        public List<Notification> Items { get; private set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.CustomerId is Guid customerId)
            {
                Items = await Service<CustomerNotifications>().For(customerId).AsNoTracking()
                    .OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
            }
            return Page();
        }

        /// <summary>Marks the notification read and goes where it points (a page of this site only).</summary>
        public async Task<IActionResult> OnPostOpenAsync(int id)
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.CustomerId is not Guid customerId) return RedirectToPage();

            var notification = await Service<CustomerNotifications>().OpenAsync(customerId, id);
            var target = notification?.RedirectUrl;
            return !string.IsNullOrWhiteSpace(target) && Url.IsLocalUrl(target) ? LocalRedirect(target) : RedirectToPage();
        }

        public async Task<IActionResult> OnPostReadAllAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.CustomerId is Guid customerId) await Service<CustomerNotifications>().MarkAllReadAsync(customerId);
            return RedirectToPage();
        }

        public static string Icon(Notification n) => n.Title switch
        {
            "Reservation approved" => "bi-check2-circle",
            "Purchase completed" => "bi-key",
            "Reservation cancelled" => "bi-x-circle",
            "A friend joined" => "bi-people",
            _ => "bi-calendar-check",
        };
    }
}
