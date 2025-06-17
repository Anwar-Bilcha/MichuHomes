using RadiantHomes.Models.Shared;

namespace RadiantHomes.Models
{
    
    public class HouseRentTransaction : BaseEntity
    {
        public Guid Id { get; set; }

        public Guid HouseId { get; set; }
        public House House { get; set; }

        // Customer who is buying or renting the house
        public Guid CustomerId { get; set; }
        public Customer Receiver { get; set; }

        // Owner of the house (seller/landlord)
        public Guid OwnerId { get; set; }
        public Customer HomeOwner { get; set; }

        // Date the transaction took place
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
        public string RentDuration { get; set; } // Monthly, Quarterly, Bi-Annually, Yearly
        public DateOnly RentStartingDate { get; set; }
        public DateOnly RentEndDate { get; set; }
        public HouseRentPayment Payment { get; set; }

        public string Status { get; set; } // e.g., Pending, ApprovedByOwner, Completed, CancelledByBorrower, Rejected

        // Optional remarks
        public string Notes { get; set; }
    }

}
