using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class ApartmentPhoto
    {
        public int PhotoId { get; set; }
        public string Name { get; set; }
        [Required]
        public string PhotoPath { get; set; }
        public Guid HouseId { get; set; }
        [ForeignKey(nameof(HouseId))]
        public ApartmentHouse House { get; set; }
    }
}