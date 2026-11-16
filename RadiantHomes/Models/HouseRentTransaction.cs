using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    
    public class HouseRentTransaction : BaseEntity
    {
        public Guid Id { get; set; }

        [Display(Name = "House")]
        public Guid HouseId { get; set; }
        public House House { get; set; }

        // Customer who is buying or renting the house
        [Display(Name = "Tenant")]
        public Guid CustomerId { get; set; }
        [Display(Name = "Tenant")]
        public Customer Receiver { get; set; }

        // Owner of the house (seller/landlord)
        [Display(Name = "Owner")]
        public Guid OwnerId { get; set; }
        [Display(Name = "Owner")]
        public Customer HomeOwner { get; set; }

        // Date the transaction took place
        [Display(Name = "Transaction date")]
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        [Display(Name = "Rent duration")]
        public string RentDuration { get; set; } // Monthly, Quarterly, Bi-Annually, Yearly
        [Display(Name = "Start date")]
        public DateOnly RentStartingDate { get; set; }
        [Display(Name = "End date")]
        public DateOnly RentEndDate { get; set; }
        public HouseRentPayment Payment { get; set; }

        public string Status { get; set; } // e.g., Pending, ApprovedByOwner, Completed, CancelledByBorrower, Rejected

        // Optional remarks
        public string Notes { get; set; }
    }

}
