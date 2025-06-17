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
    public class DetailsModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IssueOrCompliant IssueOrCompliant { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var issueorcompliant = await _context.IssuesOrCompliants.FirstOrDefaultAsync(m => m.IssueId == id);

            if (issueorcompliant is not null)
            {
                IssueOrCompliant = issueorcompliant;

                return Page();
            }

            return NotFound();
        }
    }
}
