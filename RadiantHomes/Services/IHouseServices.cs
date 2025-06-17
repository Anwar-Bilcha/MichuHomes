using RadiantHomes.Models;

namespace RadiantHomes.Services
{
    public interface IHouseServices
    {
        HouseOwnerContact GetOwnerContact(Guid HouseId);
        HouseVisitSchedule ScheduleaVisit(int HouseId);
        void ReportHouseIssue(Guid  HouseId);
        House GetHouseById(Guid HouseId);
        
    }
}
