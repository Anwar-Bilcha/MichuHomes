using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RadiantHomes.Pages.HouseOwner
{
    public class SuccessModel : PageModel
    {
        [BindProperty]
        public int DaysWait { get; set; } = 2;
        public string companyEmail { get; set; } = "info@radianthomes.com";
        public string ApplicantName { get; set; }
        public void OnGet()
        {
            ApplicantName = TempData["ApplicantName"] as string;
        }
    }
}
