using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RadiantHomes.Pages
{
    public class LiveVideoMeetModel : PageModel
    {

        public string RoomId { get; set; }

        public void OnGet(string roomId = null)
        {
            // Create a room ID or use query param
            RoomId = roomId ?? Guid.NewGuid().ToString();
        }
    }
}
