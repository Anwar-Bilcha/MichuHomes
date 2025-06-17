using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Rent.Payment
{
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public HouseRentPayment HouseRentPayment { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houserentpayment =  await _context.HouseRentPayments.FirstOrDefaultAsync(m => m.Id == id);
            if (houserentpayment == null)
            {
                return NotFound();
            }
            HouseRentPayment = houserentpayment;
           ViewData["TransactionId"] = new SelectList(_context.HouseRentTransactions, "Id", "Id");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(HouseRentPayment).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HouseRentPaymentExists(HouseRentPayment.Id))
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

        private bool HouseRentPaymentExists(Guid id)
        {
            return _context.HouseRentPayments.Any(e => e.Id == id);
        }
    }
}
