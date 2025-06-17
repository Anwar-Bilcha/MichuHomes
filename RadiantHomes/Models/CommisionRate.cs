using RadiantHomes.Models.Shared;

namespace RadiantHomes.Models
{
    public enum CommisionServiceNames
    {
        House_Rent,
        House_Sell,
        Rent_House_Get_Owner_Contact,
        House_Buy,
        Buy_House_Get_Owner_Contact,
        Monthly_Subscription_Fee,
        Quareterly_Subscription_Fee,
        SemiAnnual_Subscription_Fee,
        Annual_Subscription_Fee,
        Penality,
        Donation,
        Charity
    }
    public class CommisionRate : BaseEntity
    {
        public Guid CommisionRateId { get; set; }
        public string CommisionServiceName { get; set; }
        public string CommisionRateDescription { get; set; }
        public double CommisionPercent { get; set; }
        public CommisionCategory CommissionType { get; set; }
        public CommisionApplied OneTimeOrReccuring { get;set; }
        public double CommisionFixedAmount { get; set; }
    }

    public enum CommisionCategory
    {
        Fixed_Amount,
        Calculated_Per_Duration
    }
    public enum CommisionApplied
    {
    One_Time,
    Reccuring
    }
}
