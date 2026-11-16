using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models.HousingCompanies
{
    public class ApartmentHouseReview
    {
        public Guid Id { get; set; }
        [Display(Name = "House")]
        public Guid HouseId { get; set; }
        [ForeignKey(nameof(HouseId))]

        [Display(Name = "Apartment")]
        public ApartmentHouse ApartmentHouse { get; set; }
        [Display(Name = "Reviewer")]
        public Guid ReviewerCustomerId { get; set; }
        [ForeignKey(nameof(ReviewerCustomerId))]
        [Display(Name = "Reviewer")]
        public Customer ReviewerCustomer { get; set; }
        public string Comment { get; set; }
        public int Rating { get; set; }
        [Display(Name = "Review date")]
        public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
    }
}