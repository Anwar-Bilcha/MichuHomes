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

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public HousingCompany HousingCompany { get; set; } = default!;

        // The statuses the app works with (HousingCompanyAdmin/Create offers the same three; "Pending" is the model default)
        public static readonly string[] CompanyStatuses = { "Pending", "Active", "Inactive" };

        // CompanyStatuses, plus the stored value as "X (current value)" when it is something else, so saving keeps it
        public List<SelectListItem> StatusOptions { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var housingcompany = await _context.HousingCompanies.FirstOrDefaultAsync(m => m.CompanyId == id);
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
            LoadSelectLists(housingcompany.Status);
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("HousingCompany.Address"); // navigation property; the form posts AddressId

            if (!await _context.HousingCompanies.AnyAsync(c => c.CompanyId == HousingCompany.CompanyId))
            {
                return NotFound();
            }
            if (!(await CompanyScope.ForAsync(User, _context)).CanAccess(HousingCompany.CompanyId))
            {
                return Forbid();
            }

            // Status must be one of the known statuses, or the value already stored (kept unchanged)
            var storedStatus = await _context.HousingCompanies.AsNoTracking()
                .Where(c => c.CompanyId == HousingCompany.CompanyId)
                .Select(c => c.Status)
                .FirstOrDefaultAsync();
            if (!CompanyStatuses.Contains(HousingCompany.Status, StringComparer.Ordinal)
                && !string.Equals(HousingCompany.Status, storedStatus, StringComparison.Ordinal))
            {
                ModelState.Remove("HousingCompany.Status");
                ModelState.AddModelError("HousingCompany.Status", "Please choose Pending, Active or Inactive.");
            }

            if (!ModelState.IsValid)
            {
                LoadSelectLists(storedStatus);
                return Page();
            }

            _context.Attach(HousingCompany).State = EntityState.Modified;

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

        // Dropdowns show names, not ids
        private void LoadSelectLists(string? storedStatus)
        {
            ViewData["AddressId"] = new SelectList(_context.Addresses.AsEnumerable().OrderBy(a => a.FormattedAddressCompact), "AddressID", "FormattedAddressCompact");

            StatusOptions = CompanyStatuses.Select(s => new SelectListItem(s, s)).ToList();
            if (!string.IsNullOrEmpty(storedStatus) && !CompanyStatuses.Contains(storedStatus, StringComparer.Ordinal))
            {
                StatusOptions.Add(new SelectListItem($"{storedStatus} (current value)", storedStatus));
            }
        }

        private bool HousingCompanyExists(Guid id)
        {
            return _context.HousingCompanies.Any(e => e.CompanyId == id);
        }
    }
}
