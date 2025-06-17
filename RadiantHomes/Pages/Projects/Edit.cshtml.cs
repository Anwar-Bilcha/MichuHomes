using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Pages.Projects
{
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }
        [BindProperty]
        public HousingCompany Company { get; set; } = default!;
        [BindProperty]
        public HouseProject HouseProject { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var houseproject =  await _context.Projects.FirstOrDefaultAsync(m => m.ProjectId == id);

            if (houseproject == null)
            {
                return NotFound();
            }
            Company = _context.HousingCompanies.Find(houseproject.HousingCompanyId) ?? new();

            HouseProject = houseproject;
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            //if (!ModelState.IsValid)
            //{
            //    return Page();
            //}
            HouseProject.HousingCompanyId = Company.CompanyId;
            _context.Attach(HouseProject).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HouseProjectExists(HouseProject.ProjectId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool HouseProjectExists(Guid id)
        {
            return _context.Projects.Any(e => e.ProjectId == id);
        }
    }
}
