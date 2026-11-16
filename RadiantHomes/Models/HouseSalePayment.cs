using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;
using System.Transactions;

namespace RadiantHomes.Models
{
    public class HouseSalePayment : BaseEntity
    {
        [Key]
        public Guid Id { get; set; }
        [Display(Name = "Transaction")]
        public Guid TransactionId { get; set; }
        public HouseSaleTransaction Transaction { get; set; }
        
        [Display(Name = "House price")]
        public double HousePrice { get; set; }
        [Display(Name = "Sales commission")]
        public double SalesCommision { get; set; }
        [Display(Name = "Seller's preferred bank")]
        public string SalersPreferredBank { get; set; }
        [Display(Name = "Buyer's preferred bank")]
        public string? BuyersPreferedBank { get; set; }
        [Display(Name = "Commission bank")]
        public string? CommisionedPreferedBank { get; set; }

    }
}
