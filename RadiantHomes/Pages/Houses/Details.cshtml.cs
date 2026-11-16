using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Services;

namespace RadiantHomes.Pages.Houses
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly IHouseServices _houseServices;
        private readonly UserManager<AppUser> _userManager;

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context, IHouseServices houseServices, UserManager<AppUser> userManager)
        {
            _context = context;
            _houseServices = houseServices;
            _userManager = userManager;
        }

        /// <summary>The owner or an admin: may use the owner-only actions (Edit), same rule as Houses/Edit.</summary>
        public bool CanManage { get; set; }

        /// <summary>The signed-in user owns this house: no rent/buy/contact/report actions on their own listing.</summary>
        public bool IsOwnHouse { get; set; }
        
        public Guid HouseId { get; set; }
        public House House { get; set; } = default!;
        public HouseOwnerContact OwnerContact { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var house = await _context.Houses
                .AsNoTracking()
                .Include(h => h.HouseAddress)
                .Include(h => h.HouseCategory)
                .Include(h => h.HouseRentInfo)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (house is not null)
            {
                House = house;
                var customerId = await HouseAccess.CurrentCustomerIdAsync(_userManager, User);
                CanManage = HouseAccess.CanManage(User, customerId, house);
                IsOwnHouse = customerId == house.HouseOwnerId;

                return Page();
            }

            return NotFound();
        }
    }
}
