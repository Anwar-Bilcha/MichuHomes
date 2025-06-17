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
    public class IndexModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;

        public IndexModel(RadiantHomes.DAL.ApplicationDbContext context)
        {
            _context = context;
        }

        public List<HouseSaleTransaction> HouseSaleTransaction { get;set; } = default!;
        public House House { get; set; }
        public Customer HomeOwner { get; set; }
        public Customer Receiver { get; set; }

        public async Task OnGetAsync()
        {
            HouseSaleTransaction = await _context.HouseSaleTransactions
                .Include(h => h.HomeOwner)
                .Include(h => h.House)
                .Include(h => h.Receiver).ToListAsync();
            foreach(var transaction in HouseSaleTransaction)
            {
                HomeOwner = _context.UserProfiles.FirstOrDefault(a => a.Id == transaction.OwnerId)??new();
                House = _context.Houses.FirstOrDefault(a => a.Id == transaction.HouseId) ?? new();
                Receiver = _context.UserProfiles.FirstOrDefault(a => a.Id == transaction.CustomerId) ?? new();
            }

        }
    }
}
