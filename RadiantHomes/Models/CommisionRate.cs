using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public enum CommisionServiceNames
    {
        [Display(Name = "House rent")]
        House_Rent,
        [Display(Name = "House sale")]
        House_Sell,
        [Display(Name = "Owner contact (rent)")]
        Rent_House_Get_Owner_Contact,
        [Display(Name = "House purchase")]
        House_Buy,
        [Display(Name = "Owner contact (purchase)")]
        Buy_House_Get_Owner_Contact,
        [Display(Name = "Monthly subscription fee")]
        Monthly_Subscription_Fee,
        [Display(Name = "Quarterly subscription fee")]
        Quareterly_Subscription_Fee,
        [Display(Name = "Semi-annual subscription fee")]
        SemiAnnual_Subscription_Fee,
        [Display(Name = "Annual subscription fee")]
        Annual_Subscription_Fee,
        [Display(Name = "Penalty")]
        Penality,
        Donation,
        Charity
    }
    public class CommisionRate : BaseEntity
    {
        [Display(Name = "Commission rate")]
        public Guid CommisionRateId { get; set; }
        [Display(Name = "Service")]
        public string CommisionServiceName { get; set; }
        [Display(Name = "Description")]
        public string CommisionRateDescription { get; set; }
        [Display(Name = "Commission (%)")]
        public double CommisionPercent { get; set; }
        [Display(Name = "Commission type")]
        public CommisionCategory CommissionType { get; set; }
        [Display(Name = "One-time or recurring")]
        public CommisionApplied OneTimeOrReccuring { get;set; }
        [Display(Name = "Fixed amount")]
        public double CommisionFixedAmount { get; set; }
    }

    public enum CommisionCategory
    {
        [Display(Name = "Fixed amount")]
        Fixed_Amount,
        [Display(Name = "Calculated per duration")]
        Calculated_Per_Duration
    }
    public enum CommisionApplied
    {
    [Display(Name = "One-time")]
    One_Time,
    [Display(Name = "Recurring")]
    Reccuring
    }
}
