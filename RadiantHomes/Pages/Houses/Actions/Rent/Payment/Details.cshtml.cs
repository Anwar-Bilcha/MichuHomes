using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Rent.Payment
{
    public class DetailsModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public HouseRentPayment HouseRentPayment { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houserentpayment = await _context.HouseRentPayments.FirstOrDefaultAsync(m => m.Id == id);

            if (houserentpayment is not null)
            {
                HouseRentPayment = houserentpayment;

                return Page();
            }

            return NotFound();
        }
    }
}
