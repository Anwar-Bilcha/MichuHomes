using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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

        public DetailsModel(RadiantHomes.DAL.ApplicationDbContext context, IHouseServices houseServices)
        {
            _context = context;
            _houseServices = houseServices;
        }
        
        public Guid HouseId { get; set; }
        public House House { get; set; } = default!;
        public HouseOwnerContact OwnerContact { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var house = await _context.Houses.FirstOrDefaultAsync(m => m.Id == id);

            if (house is not null)
            {
                House = house;

                return Page();
            }

            return NotFound();
        }
    }
}
