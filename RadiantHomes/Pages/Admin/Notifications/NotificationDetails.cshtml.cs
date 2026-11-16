using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Services.NotificationService;

namespace RadiantHomes.Pages.Admin.Notifications
{
    public class NotificationDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public NotificationDetailsModel(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }
        public Notification Notification { get; set; } = default!;
        public List<ApartmentHouse> HousesCreated { get; set; } = new();

        // Approval state of what the notification is about (its houses, reservation or profile)
        public NotificationApproval Approval { get; set; } = NotificationApproval.NotApplicable;

        // The Approve button is offered for "houses pending approval" notifications that still have a house to approve
        public bool CanApproveHouses { get; set; }

        public async Task<IActionResult> OnGetAsync(int? notificationId)
        {
            var notification = notificationId > 0
                ? await _context.Notifications.FirstOrDefaultAsync(a => a.Id == notificationId)
                : null;
            if (notification == null)
            {
                TempData["ErrorMessage"] = "That notification doesn't exist or the link is invalid.";
                return RedirectToPage("./NotificationsAll");
            }
            // Real Estate Admins may only open their own company's notifications
            if (!await _notificationService.CanAccessAsync(User, notification))
            {
                return Forbid();
            }

            Notification = notification;

            // Opening a notification marks it as read
            if (!Notification.IsRead)
            {
                Notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            var subject = NotificationSubjects.Of(Notification);
            if (subject is NotificationSubject.HouseApproval or NotificationSubject.HouseReservation)
            {
                var houseIds = NotificationSubjects.ParseIds(Notification.DataKey);
                HousesCreated = await _context.ApartmentHouses
                    .Include(h => h.Photos)
                    .Where(h => houseIds.Contains(h.Id))
                    .ToListAsync();
            }

            Approval = (await _notificationService.GetApprovalStatusesAsync(new[] { Notification }))[Notification.Id];
            // Approving is a platform decision: only Michu Admins get the button
            CanApproveHouses = subject == NotificationSubject.HouseApproval
                && _notificationService.CanApproveHouses(User)
                && HousesCreated.Any(h => ApartmentHouseApproval.CanApprove(h.Status));
            return Page();
        }

        // Approves the apartment houses listed in a "houses pending approval" notification
        public async Task<IActionResult> OnPostApproveNotificationAsync(int? notificationId)
        {
            var notification = notificationId > 0
                ? await _context.Notifications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == notificationId)
                : null;
            if (notification == null || !await _notificationService.CanAccessAsync(User, notification))
            {
                TempData["ErrorMessage"] = "That notification doesn't exist or you don't have access to it.";
                return RedirectToPage("./NotificationsAll");
            }
            if (!_notificationService.CanApproveHouses(User))
            {
                TempData["ErrorMessage"] = "Only a Michu Admin can approve houses.";
                return RedirectToPage(new { notificationId });
            }

            var result = notificationId > 0
                ? await _notificationService.ApproveHousesAsync(notificationId.Value)
                : new HouseApprovalResult { NotificationFound = false };
            if (!result.NotificationFound)
            {
                TempData["ErrorMessage"] = "That notification doesn't exist or the link is invalid.";
                return RedirectToPage("./NotificationsAll");
            }

            if (!result.IsHouseApproval)
                TempData["ErrorMessage"] = "This notification doesn't ask for houses to be approved, so nothing was changed.";
            else
                TempData["SuccessMessage"] = Describe(result);

            return RedirectToPage(new { notificationId });
        }

        // "{price:N0} {currency}", the currency falling back to ETB (same convention as the public pages)
        public static string FormatPrice(ApartmentHouse house) =>
            $"{house.SalePrice:N0} {(string.IsNullOrWhiteSpace(house.Currency) ? "ETB" : house.Currency.Trim())}";

        public static string HouseStatusBadgeClass(string? status) =>
            ApartmentHouseApproval.IsApproved(status) ? "badge bg-success"
            : ApartmentHouseApproval.CanApprove(status) ? "badge bg-warning"
            : "badge bg-secondary";

        private static string Describe(HouseApprovalResult result)
        {
            static string Houses(int count) => count == 1 ? "1 house" : $"{count} houses";
            static string List(Dictionary<string, int> counts) =>
                string.Join(", ", counts.OrderByDescending(c => c.Value).Select(c => $"\"{c.Key}\" ({c.Value})"));

            if (result.HousesFound == 0)
                return "There are no houses linked to this notification to approve.";

            var parts = new List<string>
            {
                result.Approved == 0 ? "No houses needed approval." : $"{Houses(result.Approved)} approved."
            };
            if (result.Replaced.Count > 0)
                parts.Add($"Their previous status was replaced by \"{ApartmentHouseApproval.ApprovedStatus}\": {List(result.Replaced)}.");
            if (result.AlreadyApproved > 0)
                parts.Add($"{Houses(result.AlreadyApproved)} already approved (or since reserved, sold or rented) {(result.AlreadyApproved == 1 ? "was" : "were")} left as {(result.AlreadyApproved == 1 ? "it was" : "they were")}.");
            if (result.Skipped.Count > 0)
                parts.Add($"Left unchanged because of their status: {List(result.Skipped)}.");
            return string.Join(" ", parts);
        }
    }
}
