using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class HouseProject : BaseEntity
    {
        [Key] 
        public Guid ProjectId { get; set; }

        [Required(ErrorMessage = "Housing Company is required.")]
        public Guid HousingCompanyId { get; set; }

        [ForeignKey(nameof(HousingCompanyId))] 
        public HousingCompany? Company { get; set; } 

        [Required(ErrorMessage = "Address is required.")]
        [Display(Name = "Address")]
        public Guid AddressId { get; set; }

        [ForeignKey(nameof(AddressId))]
        public PartyAddress? Address { get; set; }

        [Required(ErrorMessage = "Project Name is required.")] 
        [StringLength(255, ErrorMessage = "Project Name cannot exceed 255 characters.")] 
        public string ProjectName { get; set; } = string.Empty; 

        public string? Location { get; set; } 

        public string? Description { get; set; } 

        [Required(ErrorMessage = "Start Date is required.")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Required(ErrorMessage = "Total Area is required.")] 
        [Range(0.01, double.MaxValue, ErrorMessage = "Total Area must be a positive number.")] 
        public double TotalArea { get; set; }

        public List<Building> Buildings { get; set; } = new List<Building>(); 
        public List<ApartmentPhoto> Photos { get; set; } = new List<ApartmentPhoto>(); 

    }
}