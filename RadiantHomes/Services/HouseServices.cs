using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Services
{
    public class HouseServices : IHouseServices
    {
        private readonly ApplicationDbContext _context;

        public HouseServices(ApplicationDbContext context)
        {
            _context = context;
            
        }

        public House GetHouseById(Guid HouseId)
        {
            return _context.Houses.Find(HouseId)??new();
        }

        public HouseOwnerContact GetOwnerContact(Guid HouseId)
        {
          
            var house =  _context.Houses.FirstOrDefault(h => h.Id == HouseId);
            if (house == null)
            {
                return new ();
            }
            // Implement the logic to get the house owner's contact details
            var ownerContact =  _context.UserProfiles
                .Where(c => c.Id == house.HouseOwnerId)
                .Select(c => new { c.PhoneNumber, c.Email })
                .FirstOrDefault();

            if (ownerContact != null)
            {
                return new HouseOwnerContact() {  Email = ownerContact.Email, PhoneNumber = ownerContact.PhoneNumber}; // Send owner's contact info
            }
            else
            {
                return new HouseOwnerContact(){ Message = "Owner contact info not available" };
            }
        }
        

        public void ReportHouseIssue(Guid HouseId)
        {
            throw new NotImplementedException();
        }

        public HouseVisitSchedule ScheduleaVisit(int HouseId)
        {
            throw new NotImplementedException();
        }
    }
}
