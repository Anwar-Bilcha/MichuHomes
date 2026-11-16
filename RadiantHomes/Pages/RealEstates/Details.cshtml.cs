using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Pages.RealEstates
{
    public class DetailsModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public HousingCompany HousingCompany { get; set; } = default!;

        // Financial overview. CompanyFinancialDetail rows are per budget year (sometimes duplicated per year),
        // and there is no capital figure in the model, so the overview reports revenue per budget year.
        public decimal? LatestAnnualRevenue { get; set; }
        public int? LatestBudgetYear { get; set; }
        public decimal TotalRecordedRevenue { get; set; }
        public int FirstBudgetYear { get; set; }
        public int RecordedBudgetYears { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var housingCompany = await _context.HousingCompanies
                .Include(a => a.CustomerTestimonials)
                .Include(a => a.Projects).ThenInclude(a => a.Buildings).ThenInclude(a => a.Houses)
                .Include(a => a.Address)
                .Include(a => a.FinancialDetails)
                .Include(a => a.ContactPerson) // only this company's contact persons
                .AsSplitQuery()
                .FirstOrDefaultAsync(m => m.CompanyId == id);

            if (housingCompany is null)
            {
                return NotFound();
            }

            HousingCompany = housingCompany;

            // One revenue figure per budget year: the primary record for that year if there is one, else the largest.
            var revenuePerYear = HousingCompany.FinancialDetails
                .GroupBy(f => f.BudgetYear)
                .Select(g => new
                {
                    Year = g.Key,
                    Revenue = g.OrderByDescending(f => f.IsPrimary).ThenByDescending(f => f.AnnualRevenue).First().AnnualRevenue
                })
                .OrderBy(r => r.Year)
                .ToList();

            if (revenuePerYear.Any())
            {
                var latest = revenuePerYear.Last();
                LatestAnnualRevenue = latest.Revenue;
                LatestBudgetYear = latest.Year > 0 ? latest.Year : null;
                var knownYears = revenuePerYear.Where(r => r.Year > 0).ToList();
                RecordedBudgetYears = knownYears.Count;
                if (knownYears.Any())
                {
                    TotalRecordedRevenue = knownYears.Sum(r => r.Revenue);
                    FirstBudgetYear = knownYears.First().Year;
                }
            }

            return Page();
        }
    }
}
