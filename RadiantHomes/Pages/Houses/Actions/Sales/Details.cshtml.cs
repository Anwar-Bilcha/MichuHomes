using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Sales
{
    public class DetailsModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public HouseSaleTransaction HouseSaleTransaction { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var housesaletransaction = await _context.HouseSaleTransactions.FirstOrDefaultAsync(m => m.Id == id);

            if (housesaletransaction is not null)
            {
                HouseSaleTransaction = housesaletransaction;

                return Page();
            }

            return NotFound();
        }
    }
}
