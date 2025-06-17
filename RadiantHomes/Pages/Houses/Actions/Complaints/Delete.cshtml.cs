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
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
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

        public async Task<IActionResult> OnPostAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var issueorcompliant = await _context.IssuesOrCompliants.FindAsync(id);
            if (issueorcompliant != null)
            {
                IssueOrCompliant = issueorcompliant;
                _context.IssuesOrCompliants.Remove(IssueOrCompliant);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
