using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public class HomeOwnerRanking : BaseEntity
    {

        [Key]
        public Guid RankingId { get; set; }
        public string RankName { get; set; } = HouseOwnerRanks.New.ToString();
        public bool? PhoneVerified { get; set; }
        public bool? IdVerified { get; set; }
        public bool? AddressVerified { get; set; }
        public bool? PaymentVerified  { get; set; }
        public bool? SoldHouseBefore { get; set; }
        public bool? BoughtHouseBefore { get; set; }
        public List<Customer>? Owner { get; set; }
        public List<Customer>? HouseHolder { get; set; }
    }
    public enum HouseOwnerRanks
    {
        New,
        Phone_Verified,
        Phone_And_Address_Verified,
        Verified_Seller,
        Verified_Buyer,
        PremiumCustomer
    }
}