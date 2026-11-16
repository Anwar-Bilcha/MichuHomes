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
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IList<IssueOrCompliant> IssueOrCompliant { get;set; } = default!;

        /// <summary>Edit/Delete buttons: only the complainant or a Michu admin may change a complaint.</summary>
        public bool CanChange(IssueOrCompliant issue) => ComplaintAccess.CanChange(User, _customerId, issue);
        private Guid? _customerId;

        public async Task OnGetAsync()
        {
            // Michu admins see every complaint; everyone else those about their own houses.
            _customerId = await HouseAccess.CurrentCustomerIdAsync(_userManager, User);
            IssueOrCompliant = await _context.IssuesOrCompliants
                .VisibleTo(TransactionAccess.IsAdmin(User), _customerId)
                .Include(i => i.House)
                .Include(i => i.HouseOwner).ToListAsync();
        }
    }
}
