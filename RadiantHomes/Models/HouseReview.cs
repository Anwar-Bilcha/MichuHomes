using RadiantHomes.Models.HousingCompanies;

namespace RadiantHomes.Models
{
    public class HouseReview
    {
        public Guid Id { get; set; }
        public Guid HouseId { get; set; }
        public House House { get; set; }
        public ApartmentHouse ApartmentHouse { get; set; }
        public Guid ReviewerCustomerId { get; set; } // Optional link to a customer
        public Customer ReviewerCustomer { get; set; }
        public string Comment { get; set; }
        public int Rating { get; set; } // 1–5 stars, for example
        public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
    }
}
