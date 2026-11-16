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

namespace RadiantHomes.Pages.Houses.Actions.Rent
{
    public class DetailsModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public HouseRentTransaction HouseRentTransaction { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var houserenttransaction = await _context.HouseRentTransactions
                .AsNoTracking()
                .Include(t => t.House)
                .Include(t => t.Receiver)
                .Include(t => t.HomeOwner)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (houserenttransaction is null)
            {
                return NotFound();
            }
            if (!await CanAccessAsync(houserenttransaction))
            {
                return Forbid();
            }

            HouseRentTransaction = houserenttransaction;
            return Page();
        }

        /// <summary>A Michu admin or a party to the rent transaction (owner / tenant).</summary>
        private Task<bool> CanAccessAsync(HouseRentTransaction record) =>
            TransactionAccess.CanAccessAsync(_userManager, User, record.OwnerId, record.CustomerId);
    }
}
