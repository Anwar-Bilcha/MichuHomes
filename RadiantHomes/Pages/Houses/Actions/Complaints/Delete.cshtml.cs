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
    public class DeleteModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public DeleteModel(RadiantHomes.DAL.ApplicationDbContext context, UserManager<AppUser> userManager, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        [BindProperty]
        public IssueOrCompliant IssueOrCompliant { get; set; } = default!;

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
            // Only the complainant or a Michu admin may delete a complaint.
            if (!ComplaintAccess.CanChange(User, await HouseAccess.CurrentCustomerIdAsync(_userManager, User), issueorcompliant))
            {
                return Forbid();
            }
            IssueOrCompliant = issueorcompliant;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var issueorcompliant = await _context.IssuesOrCompliants.FindAsync(id);
            if (issueorcompliant == null)
            {
                return RedirectToPage("./Index");
            }
            if (!ComplaintAccess.CanChange(User, await HouseAccess.CurrentCustomerIdAsync(_userManager, User), issueorcompliant))
            {
                return Forbid();
            }

            _context.IssuesOrCompliants.Remove(issueorcompliant);
            await _context.SaveChangesAsync();
            UploadedFiles.DeleteFolder(_webHostEnvironment.WebRootPath, $"uploads/complaints/{issueorcompliant.IssueId}"); // its evidence files
            TempData["Success"] = "Complaint deleted.";
            return RedirectToPage("./Index");
        }
    }
}
