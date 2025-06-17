using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public class HouseRentPayment : BaseEntity
    {
        [Key]
        public Guid Id { get; set; }
        public Guid TransactionId { get; set; }
        public HouseRentTransaction Transaction { get; set; }
        public double RentCommision { get; set; }
        public double RentPricePerTerm { get; set; }
        public double TotalRentPrice { get; set; }
        public string? SalersPreferredBank { get; set; }
        public string? BuyersPreferedBank { get; set; }
        public string? CommisionedPreferedBank { get; set; }

    }
}
