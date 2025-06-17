using RadiantHomes.DAL;
using RadiantHomes.Models;
using SQLitePCL;

namespace RadiantHomes.Services
{
    public interface ICustomerService
    {
        Customer GetCustomerByEmail(string email);
    }
    public class CustomerService : ICustomerService
    {
        private readonly ApplicationDbContext _context;

        public CustomerService(ApplicationDbContext context)
        {
            _context = context;
        }
        public Customer GetCustomerByEmail(string email)
        {
            return _context.UserProfiles.FirstOrDefault(a => a.Email == email) ?? new();
        }
    }
}
