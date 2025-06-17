using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.DAL; // Assuming your ApplicationDbContext is in this namespace
using System.Linq;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages
{
    //[Authorize(Roles = "HousingCompanyAdmin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;

        public IndexModel(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public int ActiveProjectCount { get; set; } = -1; // Initialize to -1 to indicate it hasn't been loaded

        public void OnGet()
        {
            // Fetch the count of active projects from the database
            ActiveProjectCount = _dbContext.Projects.Count(p => p.EndDate <= DateTime.Now);
        }
    }
}