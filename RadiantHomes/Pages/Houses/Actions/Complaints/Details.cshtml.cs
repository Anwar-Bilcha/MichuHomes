using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Complaints
{
    public class DetailsModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IssueOrCompliant IssueOrCompliant { get; set; } = default!;

        /// <summary>May edit or delete it (the complainant or a Michu admin).</summary>
        public bool CanChange { get; private set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var issueorcompliant = await _context.IssuesOrCompliants
                .Include(i => i.House)
                .Include(i => i.HouseOwner)
                .FirstOrDefaultAsync(m => m.IssueId == id);

            if (issueorcompliant is null)
            {
                return NotFound();
            }
            // Visible to a Michu admin, the complainant and the owner of the house it is about.
            var customerId = await HouseAccess.CurrentCustomerIdAsync(_userManager, User);
            if (!ComplaintAccess.CanView(User, customerId, issueorcompliant))
            {
                return Forbid();
            }
            IssueOrCompliant = issueorcompliant;
            CanChange = ComplaintAccess.CanChange(User, customerId, issueorcompliant);
            return Page();
        }
    }
}
