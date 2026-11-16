using RadiantHomes.Models.HousingCompanies;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public class HouseReview
    {
        public Guid Id { get; set; }
        [Display(Name = "House")]
        public Guid HouseId { get; set; }
        public House House { get; set; }
        [Display(Name = "Apartment")]
        public ApartmentHouse ApartmentHouse { get; set; }
        [Display(Name = "Reviewer")]
        public Guid ReviewerCustomerId { get; set; } // Optional link to a customer
        [Display(Name = "Reviewer")]
        public Customer ReviewerCustomer { get; set; }
        public string Comment { get; set; }
        public int Rating { get; set; } // 1–5 stars, for example
        [Display(Name = "Review date")]
        public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
    }
}
