using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Services.CustomerPortal
{
    /// <summary>
    /// Notifications for customers (shown in the customer portal). They use the existing Notifications table:
    /// RecipientRole = Buyer and ReceiverAdminId = the customer's profile id. Admin notification lists leave them out
    /// (NotificationService.GetForMichuAdminAsync), company lists only show their own company's notifications.
    /// </summary>
    public sealed class CustomerNotifications
    {
        public const string RecipientRole = MichuHomesRoles.Buyer;

        private readonly ApplicationDbContext _context;

        public CustomerNotifications(ApplicationDbContext context) => _context = context;

        /// <summary>Adds a notification to the context; the caller saves it together with the change it reports.</summary>
        public void Add(Guid customerId, string title, string message, EntityAffecteds about, string dataKey, string? url) =>
            _context.Notifications.Add(new Notification
            {
                Title = title,
                Message = message,
                ReceiverAdminId = customerId,
                RecipientRole = RecipientRole,
                Datatype = about.ToString(),
                DataKey = dataKey,
                RedirectUrl = url,
                CreatedAt = DateTime.Now,
                IsRead = false,
            });

        public IQueryable<Notification> For(Guid customerId) =>
            _context.Notifications.Where(n => n.RecipientRole == RecipientRole && n.ReceiverAdminId == customerId);

        public Task<int> UnreadCountAsync(Guid customerId) => For(customerId).CountAsync(n => !n.IsRead);

        /// <summary>Marks one of the customer's notifications read and returns it (null if it isn't theirs).</summary>
        public async Task<Notification?> OpenAsync(Guid customerId, int id)
        {
            var notification = await For(customerId).FirstOrDefaultAsync(n => n.Id == id);
            if (notification is { IsRead: false })
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return notification;
        }

        public Task<int> MarkAllReadAsync(Guid customerId) =>
            For(customerId).Where(n => !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}
