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
        Task MarkAsReadAsync(int notificationId);
    }

}
