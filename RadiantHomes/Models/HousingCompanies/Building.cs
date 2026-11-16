using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class Building:BaseEntity
    {
        [Key]
        public Guid Id { get; set; }
        [Required(ErrorMessage = "The Project for this building is required.")]
        [Display(Name = "Project")]
        public Guid ProjectId { get; set; }
        [ForeignKey(nameof(ProjectId))]
        public HouseProject? Project { get; set; }
        [Display(Name = "Address")]
        public Guid AddressId { get; set; }
        [Required(ErrorMessage = "Address information is required.")]
        [ForeignKey(nameof(AddressId))]
        public PartyAddress? Address { get; set; }
        [Required(ErrorMessage = "Building Name is required.")]
        [Display(Name = "Building name")]
        public string BuildingName { get; set; }
        [Required(ErrorMessage = "Number of floors for the Building is required.")]
        [Display(Name = "Number of floors")]
        public int NumberOfFloors { get; set; }
        [Required(ErrorMessage = "Building Type for the Building is required.")]
        [Display(Name = "Building type")]
        public string BuildingType { get; set; } = BuildingTypes.Residential.ToString();
        [Required(ErrorMessage = "Construction status for the Building is required.")]
        [Display(Name = "Construction status")]
        public string ConstructionStatus { get; set; } = ConstructionStatuses.Completed.ToString();
        public List<ApartmentHouse> Houses { get; set; } = new List<ApartmentHouse>();
        public List<ApartmentPhoto> Photos { get; set; } = new List<ApartmentPhoto>();
        [Required(ErrorMessage = "Mumber of houses for each floors of the Building is required.")] 
        [Display(Name = "Houses per floor")]
        public List<HousePerFloor> TotalHousesPerFloor { get; set; } = new List<HousePerFloor>();

    }

}
