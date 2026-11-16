using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public class RentInfo
    {
        public Guid Id { get; set; }
        [Display(Name = "House")]
        public Guid HouseId { get; set; }
        public House House { get; set; }
        [Display(Name = "On rent")]
        public bool IsOnRent { get; set; }
        [Display(Name = "Rent term")]
        public RentTerms RentTerm { get; set; }
        [Display(Name = "Price per term")]
        public double PricePerTerm { get; set; }
        [Display(Name = "Expected advance payment")]
        public double ExcepectedAdvancePayment { get; set; }
        public enum RentTerms
        {
            Monthly,
            Quarterly,
            [Display(Name = "Semi-annually")]
            Semi_Annually,
            Annually
        }
    }
}