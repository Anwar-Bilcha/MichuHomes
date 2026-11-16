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

namespace RadiantHomes.Pages.Houses.Actions.Sales
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

        public HouseSaleTransaction HouseSaleTransaction { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var housesaletransaction = await _context.HouseSaleTransactions
                .AsNoTracking()
                .Include(t => t.House)
                .Include(t => t.Receiver)
                .Include(t => t.HomeOwner)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (housesaletransaction is null)
            {
                return NotFound();
            }
            if (!await CanAccessAsync(housesaletransaction))
            {
                return Forbid();
            }

            HouseSaleTransaction = housesaletransaction;
            return Page();
        }

        /// <summary>A Michu admin or a party to the house sale (seller / buyer).</summary>
        private Task<bool> CanAccessAsync(HouseSaleTransaction record) =>
            TransactionAccess.CanAccessAsync(_userManager, User, record.OwnerId, record.CustomerId);
    }
}
