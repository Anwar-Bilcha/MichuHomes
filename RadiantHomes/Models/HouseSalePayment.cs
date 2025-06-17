using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;
using System.Transactions;

namespace RadiantHomes.Models
{
    public class HouseSalePayment : BaseEntity
    {
        [Key]
        public Guid Id { get; set; }
        public Guid TransactionId { get; set; }
        public HouseSaleTransaction Transaction { get; set; }
        
        public double HousePrice { get; set; }
        public double SalesCommision { get; set; }
        public string SalersPreferredBank { get; set; }
        public string? BuyersPreferedBank { get; set; }
        public string? CommisionedPreferedBank { get; set; }

    }
}
