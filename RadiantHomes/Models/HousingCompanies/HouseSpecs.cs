using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class HouseSpecs
    {
        [Key]
        public Guid Id { get; set; }
        public Guid HouseId { get; set; }
        [ForeignKey(nameof(HouseId))]
        public ApartmentHouse House { get; set; }
        public string SpecificationCriteria { get; set; }
        public string SpecificationDescription { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}