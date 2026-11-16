using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class HouseSpecs
    {
        [Key]
        public Guid Id { get; set; }
        [Display(Name = "House")]
        public Guid HouseId { get; set; }
        [ForeignKey(nameof(HouseId))]
        public ApartmentHouse House { get; set; }
        [Display(Name = "Specification")]
        public string SpecificationCriteria { get; set; }
        [Display(Name = "Description")]
        public string SpecificationDescription { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}