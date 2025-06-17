using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Pages.Projects
{
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        [TempData]
        public string? SuccessMessage { get; set; }
        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }
        [BindProperty(SupportsGet = true)] public Guid CompanyId { get; set; }

        public HousingCompany Company { get; set; } = new HousingCompany();
        public IActionResult OnGet(Guid companyId)
        {
            CompanyId = companyId;
            Company = _context.HousingCompanies.Find(companyId) ?? new();
            if (!TempData.ContainsKey("SuccessMessage"))
                SuccessMessage = null;

            if (CompanyId == default)
            {
                return NotFound();
            }
            return Page();
        }

        [BindProperty]
        public HouseProject HouseProject { get; set; }

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            //if (!ModelState.IsValid)
            //{
            //    return Page();
            //}
            HouseProject.CreatedOn = DateTime.Now;
            HouseProject.CreatedBy = "A";
            HouseProject.UpdatedBy = "A";
            HouseProject.UpdatedOn = new DateTime();
            HouseProject.HousingCompanyId = CompanyId;
            _context.Projects.Add(HouseProject);
            await _context.SaveChangesAsync();
            SuccessMessage = "Project created successfully!";
            return Page();
        }
    }
}
