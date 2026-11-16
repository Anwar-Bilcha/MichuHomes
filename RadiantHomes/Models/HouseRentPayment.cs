using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public class HouseRentPayment : BaseEntity
    {
        [Key]
        public Guid Id { get; set; }
        [Display(Name = "Transaction")]
        public Guid TransactionId { get; set; }
        public HouseRentTransaction Transaction { get; set; }
        [Display(Name = "Rent commission")]
        public double RentCommision { get; set; }
        [Display(Name = "Rent price per term")]
        public double RentPricePerTerm { get; set; }
        [Display(Name = "Total rent price")]
        public double TotalRentPrice { get; set; }
        [Display(Name = "Owner's preferred bank")]
        public string? SalersPreferredBank { get; set; }
        [Display(Name = "Tenant's preferred bank")]
        public string? BuyersPreferedBank { get; set; }
        [Display(Name = "Commission bank")]
        public string? CommisionedPreferedBank { get; set; }

    }
}
