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

namespace RadiantHomes.Pages.Address
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<PartyAddress> PartyAddress { get;set; } = default!;

        public async Task OnGetAsync()
        {
            PartyAddress = await _context.Addresses.OrderBy(a=>a.CreatedOn).ToListAsync();
        }
    }
}
