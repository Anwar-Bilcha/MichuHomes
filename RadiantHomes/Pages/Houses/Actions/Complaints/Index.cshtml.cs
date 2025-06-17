using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Complaints
{
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<IssueOrCompliant> IssueOrCompliant { get;set; } = default!;

        public async Task OnGetAsync()
        {
            IssueOrCompliant = await _context.IssuesOrCompliants
                .Include(i => i.House)
                .Include(i => i.HouseOwner).ToListAsync();
        }
    }
}
