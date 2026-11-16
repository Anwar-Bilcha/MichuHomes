using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class ApartmentPhoto
    {
        [Display(Name = "Photo")]
        public int PhotoId { get; set; }
        public string Name { get; set; }
        [Required]
        [Display(Name = "Photo")]
        public string PhotoPath { get; set; }
        [Display(Name = "House")]
        public Guid HouseId { get; set; }
        [ForeignKey(nameof(HouseId))]
        public ApartmentHouse House { get; set; }
    }
}