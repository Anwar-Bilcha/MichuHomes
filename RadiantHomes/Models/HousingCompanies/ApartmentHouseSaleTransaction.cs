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
        [Display(Name = "Apartment")]
        public Guid ApartmentHouseId { get; set; }
        [ForeignKey(nameof(ApartmentHouseId))]
        [Display(Name = "Apartment")]
        public ApartmentHouse ApartmentHouse { get; set; }
        [Display(Name = "Buyer")]
        public Guid CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        [Display(Name = "Buyer")]
        public Customer Receiver { get; set; }

        [Display(Name = "Seller")]
        public Guid OwnerId { get; set; }
        [ForeignKey(nameof(OwnerId))]

        [Display(Name = "Seller")]
        public Customer HomeOwner { get; set; }

        [Display(Name = "Transaction date")]
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
