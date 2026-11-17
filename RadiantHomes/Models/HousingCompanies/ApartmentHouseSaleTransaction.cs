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

        /// <summary>Short reference shown to customers and staff (e.g. R-2611-7K3QP).</summary>
        [MaxLength(20)]
        [Display(Name = "Reference")]
        public string Reference { get; set; } = string.Empty;
        /// <summary>The selling company (copied from the apartment's project when reserved).</summary>
        public Guid? CompanyId { get; set; }
        /// <summary>A pending reservation expires at this time unless the company answers (Admin/Settings). Null: never.</summary>
        [Display(Name = "Expires")]
        public DateTime? ExpiresAt { get; set; }
        /// <summary>Concurrency token: two people changing the same reservation at once can't both win.</summary>
        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public List<ReservationStatusChange> History { get; set; } = new();
    }
    public enum HouseReserveStatuses
    {
        Pending, Approved, Completed, Cancelled, Closed,
        /// <summary>Not answered by the company in time (pending reservations only).</summary>
        Expired,
        /// <summary>Declined by the company, with a reason.</summary>
        Rejected
    }
}
