using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class HousePerFloor
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage ="Building is required")]
        public Guid BuildingId { get; set; }
        [ForeignKey(nameof(BuildingId))]
        public Building? Building { get; set; }
        [Required(ErrorMessage = "Floor Number is required")]
        [Display(Name ="Floor Number")]
        public int FloorNumber { get; set; }
        [Required(ErrorMessage = "Houses Number for a floor is required")]
        [Display(Name = "Total Houses")]
        public int NumberOfHouses { get; set; }
    }
}