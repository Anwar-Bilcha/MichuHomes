using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadiantHomes.Models.Shared;

namespace RadiantHomes.Models
{
    /// <summary>
    /// A code a customer shares with friends (customer portal: "Refer a friend"). A friend who registers with the
    /// code's link becomes a <see cref="Referral"/>. A customer can have several codes, e.g. one per group of friends.
    /// </summary>
    public class ReferralCode : BaseEntity
    {
        public Guid Id { get; set; }

        /// <summary>Short, unique, easy to read aloud: no 0/O or 1/I (see ReferralService).</summary>
        [Required, StringLength(20)]
        public string Code { get; set; } = string.Empty;

        public Guid CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }

        /// <summary>The customer's own note, e.g. "Family" or "Office friends".</summary>
        [StringLength(60)]
        [Display(Name = "Label")]
        public string? Label { get; set; }

        /// <summary>Switched-off codes no longer register new referrals; earlier ones stay.</summary>
        public bool IsActive { get; set; } = true;

        public List<Referral> Referrals { get; set; } = new();
    }

    /// <summary>A friend who registered with a referral code (CreatedOn = when they joined).</summary>
    public class Referral : BaseEntity
    {
        public Guid Id { get; set; }

        public Guid ReferralCodeId { get; set; }

        [ForeignKey(nameof(ReferralCodeId))]
        public ReferralCode? ReferralCode { get; set; }

        /// <summary>The friend's account (AspNetUsers.Id). No foreign key: accounts are managed by ASP.NET Identity.</summary>
        [Required, StringLength(450)]
        public string ReferredUserId { get; set; } = string.Empty;
    }
}
