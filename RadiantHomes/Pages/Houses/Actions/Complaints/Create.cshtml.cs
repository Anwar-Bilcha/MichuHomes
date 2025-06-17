using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Complaints
{
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
        ViewData["HouseId"] = new SelectList(_context.Houses, "Id", "Id");
        ViewData["HouseOwnerId"] = new SelectList(_context.UserProfiles, "Id", "Id");
            return Page();
        }

        [BindProperty]
        public IssueOrCompliant IssueOrCompliant { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            //if (!ModelState.IsValid)
            //{
            //    return Page();
            //}

            _context.IssuesOrCompliants.Add(IssueOrCompliant);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
