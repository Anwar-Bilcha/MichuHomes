using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Pages.Admin.Notifications
{
    public class NotificationDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public NotificationDetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }
        public Notification Notification { get; set; } = default!;
        public List<ApartmentHouse> HousesCreated { get; set; } = new();
        public async Task<IActionResult> OnGetAsync(int notificationId)
        {
            if(notificationId>0)
            {
                Notification =  _context.Notifications.FirstOrDefault(a=>a.Id==notificationId)??new();
                Notification.IsRead = true;
               await _context.SaveChangesAsync();
            }
            string[] housesId = Notification.DataKey.Split(",");
            
            for(int i=0; i<housesId.Length-1;i++)
            {
                Guid HouseId = Guid.Parse(housesId[i].Trim());
                var house = _context.ApartmentHouses.FirstOrDefault(a => a.Id == HouseId);
                if (house != null)
                {
                    HousesCreated.Add(house);
                }
            }
            return Page();
        }
        public async Task<IActionResult> OnApproveNotificationAsync(int notificationId)
        {
            if(notificationId>0)
            {
                Notification = await _context.Notifications.FirstOrDefaultAsync(a => a.Id == notificationId) ?? new();
            }
            var housesId= Notification.DataKey.Split(",");
            foreach (var houseId in housesId)
            {
                Guid HouseId = Guid.Parse(houseId);
                var house = _context.ApartmentHouses.FirstOrDefault(a => a.Id == HouseId);
                if (house != null)
                {
                    house.Status = "Approved";
                    _context.SaveChanges();
                }
            }
            return Page();
        }
    }
}
