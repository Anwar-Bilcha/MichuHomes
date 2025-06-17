using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class ApartmentHouseSaleTransaction : BaseEntity
    {
        [Key]
        public Guid Id { get; set; }               
        public Guid ApartmentHouseId { get; set; }
        [ForeignKey(nameof(ApartmentHouseId))]
        public ApartmentHouse ApartmentHouse { get; set; }
        public Guid CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public Customer Receiver { get; set; }

        public Guid OwnerId { get; set; }
        [ForeignKey(nameof(OwnerId))]

        public Customer HomeOwner { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        public HouseReserveStatuses Status { get; set; } = HouseReserveStatuses.Pending; 

        // Optional remarks
        public string Notes { get; set; }
    }
    public enum HouseReserveStatuses
    {
        Pending, Approved, Completed, Cancelled, Closed
    }
}
