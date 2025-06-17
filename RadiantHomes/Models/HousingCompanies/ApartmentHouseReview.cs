using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class ApartmentHouseReview
    {
        public Guid Id { get; set; }
        public Guid HouseId { get; set; }
        [ForeignKey(nameof(HouseId))]

        public ApartmentHouse ApartmentHouse { get; set; }
        public Guid ReviewerCustomerId { get; set; }
        [ForeignKey(nameof(ReviewerCustomerId))]
        public Customer ReviewerCustomer { get; set; }
        public string Comment { get; set; }
        public int Rating { get; set; }
        public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
    }
}