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
using RadiantHomes.Services.AdminServices;

namespace RadiantHomes.Pages.RealEstates
{
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public HousingCompany HousingCompany { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var housingcompany = await _context.HousingCompanies.AsNoTracking().FirstOrDefaultAsync(m => m.CompanyId == id);
            if (housingcompany == null)
            {
                return NotFound();
            }
            // Real Estate Admins may only edit their own company
            if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(housingcompany.CompanyId))
            {
                return Forbid();
            }
            HousingCompany = housingcompany;
            await PopulateAddressesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (HousingCompany == null)
            {
                return BadRequest();
            }

            var existing = await _context.HousingCompanies.FirstOrDefaultAsync(c => c.CompanyId == HousingCompany.CompanyId);
            if (existing == null)
            {
                return NotFound();
            }
            if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(existing.CompanyId))
            {
                return Forbid();
            }

            // The saved status stays acceptable even if it isn't one of the standard ones (older records).
            await CompanyForm.ValidateAsync(_context, ModelState, HousingCompany, savedStatus: existing.Status);
            if (!ModelState.IsValid)
            {
                await PopulateAddressesAsync();
                return Page();
            }

            // Only the fields on the form change; CreatedOn/CreatedBy are kept and UpdatedOn/UpdatedBy are stamped
            // by ApplicationDbContext.
            existing.Name = HousingCompany.Name;
            existing.LicenseNumber = HousingCompany.LicenseNumber;
            existing.EstablishedDate = HousingCompany.EstablishedDate;
            existing.CompanyCategory = HousingCompany.CompanyCategory;
            existing.AddressId = HousingCompany.AddressId;
            existing.Email = HousingCompany.Email;
            existing.PhoneNumber = HousingCompany.PhoneNumber;
            existing.Website = HousingCompany.Website;
            existing.LogoUrl = HousingCompany.LogoUrl;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HousingCompanyExists(HousingCompany.CompanyId))
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

        private async Task PopulateAddressesAsync() =>
            ViewData["AddressId"] = await CompanyForm.AddressSelectListAsync(_context, HousingCompany?.AddressId);

        private bool HousingCompanyExists(Guid id)
        {
            return _context.HousingCompanies.Any(e => e.CompanyId == id);
        }
    }
}
