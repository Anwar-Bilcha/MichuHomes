using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Shared;

namespace RadiantHomes.Models
{
    public class HouseSaleTransaction : BaseEntity
    {
        public Guid Id { get; set; }

        // Type of transaction: Rent or Purchase

        // House involved in the transaction
        public Guid HouseId { get; set; }
        public House? House { get; set; }

        // Customer who is buying or renting the house
        public Guid CustomerId { get; set; }
        public Customer Receiver { get; set; }

        // Owner of the house (seller/landlord)
        public Guid OwnerId { get; set; }
        public Customer HomeOwner { get; set; }

        // Date the transaction took place
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        //// For rentals: lease start and end
        //public DateTime? RentStartDate { get; set; }
        //public DateTime? RentEndDate { get; set; }

        public HouseSalePayment Payment { get; set; }
        // Total price or rental cost
        //public decimal Amount { get; set; }

        //// Optional payment info
        //public string PaymentMethod { get; set; } // e.g., Bank Transfer, Mobile Money
        //public string PaymentReference { get; set; }

        // Status of the transaction
        public string Status { get; set; } // e.g., Pending, Completed, Cancelled

        // Optional remarks
        public string Notes { get; set; }
    }

}
