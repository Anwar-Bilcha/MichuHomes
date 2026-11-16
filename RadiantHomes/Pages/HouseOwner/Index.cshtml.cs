using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Services.AdminServices;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RadiantHomes.Pages.HouseOwner
{
    // Access: Michu Admin or Real Estate Admin (Program.cs). Profiles contain personal data, so a
    // Real Estate Admin only sees the profiles linked to their own company.
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Customer> Customers { get; set; } = new List<Customer>();

        public async Task OnGetAsync()
        {
            var scope = await CompanyScope.ForAsync(User, _context);
            var query = _context.UserProfiles.AsQueryable();

            if (scope.IsRestricted)
            {
                if (scope.CompanyId == null)
                {
                    Customers = new List<Customer>();
                    return;
                }
                query = query.Where(c => c.CompanyId == scope.CompanyId);
            }

            Customers = await query.ToListAsync();
        }
    }
}
