using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public class Subscriber
    {
        public Guid Id { get; set; }
        [EmailAddress(ErrorMessage ="Email should be a valid email address")]
        public string Email { get; set; }
        [Display(Name = "Subscription type")]
        public string SubscriptionType { get; set; } = SubscriptionTypes.Daily.ToString();
    }
    public enum SubscriptionTypes
    {
        None,
        Daily,
        Weekly,
        Automatic
    }
}
