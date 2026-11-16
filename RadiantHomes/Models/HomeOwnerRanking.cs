using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public class HomeOwnerRanking : BaseEntity
    {

        [Key]
        [Display(Name = "Ranking")]
        public Guid RankingId { get; set; }
        [Display(Name = "Rank")]
        public string RankName { get; set; } = HouseOwnerRanks.New.ToString();
        [Display(Name = "Phone verified")]
        public bool? PhoneVerified { get; set; }
        [Display(Name = "ID verified")]
        public bool? IdVerified { get; set; }
        [Display(Name = "Address verified")]
        public bool? AddressVerified { get; set; }
        [Display(Name = "Payment verified")]
        public bool? PaymentVerified  { get; set; }
        [Display(Name = "Sold a house before")]
        public bool? SoldHouseBefore { get; set; }
        [Display(Name = "Bought a house before")]
        public bool? BoughtHouseBefore { get; set; }
        public List<Customer>? Owner { get; set; }
        public List<Customer>? HouseHolder { get; set; }
    }
    public enum HouseOwnerRanks
    {
        New,
        [Display(Name = "Phone verified")]
        Phone_Verified,
        [Display(Name = "Phone and address verified")]
        Phone_And_Address_Verified,
        [Display(Name = "Verified seller")]
        Verified_Seller,
        [Display(Name = "Verified buyer")]
        Verified_Buyer,
        [Display(Name = "Premium customer")]
        PremiumCustomer
    }
}