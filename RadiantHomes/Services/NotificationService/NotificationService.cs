using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Services.NotificationService
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }
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
            return _context.Notifications
                .Where(n => !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
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
            return await _context.Notifications.Where(a => a.ReceiverCompanyId == companyId || a.ReceiverAdminId==companyId && a.IsRead==false).ToListAsync();
        }

        async Task INotificationService.MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(a => a.Id == notificationId);
            if(notification!=null)
            {
                notification.IsRead=true;
            }
        }
    }

}
