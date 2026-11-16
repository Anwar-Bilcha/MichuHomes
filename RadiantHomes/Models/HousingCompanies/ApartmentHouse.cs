using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{   
    public class ApartmentHouse : BaseEntity
    {
    public Guid Id { get; set; }
    public string Title { get; set; }
    [Display(Name = "House number")]
    public string HouseNumber { get; set; } = "New";
    public string? Description { get; set; }
    [Display(Name = "Sale price")]
    public double SalePrice { get; set; }
    [Display(Name = "Specifications")]
    public List<HouseSpecs> HouseSpecs { get; set; }
    [Display(Name = "Contact person")]
    public Guid? ContactPersonId { get; set; }
    [ForeignKey(nameof(ContactPersonId))]
        [Required(ErrorMessage = "House Contact Person is required.")]
        [Display(Name = "Contact person")]
        public Customer? ContactPerson { get; set; }
    [Display(Name = "Category")]
    public Guid HouseCategoryId { get; set; } // Apartment, Villa, etc
    [ForeignKey(nameof(HouseCategoryId))]
    [Required(ErrorMessage = "House Category is required.")]
    [Display(Name = "Category")]
    public HouseCategory HouseCategory { get; set; }
    [Display(Name = "Available for")]
    public string? AvailableFor { get; set; } // Rent or Sale
        [Required(ErrorMessage = "House Address is required.")]
        [Display(Name = "Address")]
        public Guid AddressId { get; set; }
        [ForeignKey(nameof(AddressId))]
        [Display(Name = "Address")]
        public PartyAddress HouseAddress { get; set; }
           
    public string? Status { get; set; } = "Pending"; // New, Active, Hidden, Sold, Rented etc
        [Display(Name = "Sales status")]
        public string SalesStatus { get; set; } = "None";
        public List<ApartmentHouseSaleTransaction> SaleTransactions { get; set; } = new List<ApartmentHouseSaleTransaction>();
    public List<IssueOrCompliant>? IssuesOrReportings { get; set; }
    public List<ApartmentHouseReview>? Reviews { get; set; }
    [Required(ErrorMessage = "Building for the House is required.")]
    [Display(Name = "Building")]
    public Guid BuildingId { get; set; }
    [Display(Name = "House type")]
    public string HouseType { get; set; } = HouseTypesCateg.Residential.ToString();
    [Display(Name = "Floor number")]
    public int FloorNumber { get; set; }
    [ForeignKey(nameof(BuildingId))]
    public Building? Building { get; set; }
    public int Bathrooms { get; set; }
    [Display(Name = "Total rooms")]
    public int TotalRooms { get; set; }
    public double Area { get; set; }
    [Display(Name = "Available")]
    public bool IsAvailable { get; set; }
    public string Currency { get; set; } = "ETB";
    [Display(Name = "Living rooms")]
    public int LivingRooms { get; set; }
    public int Bedrooms { get; set; }

    [Display(Name = "Has balcony")]
    public bool HasBalcony { get; set; }

    [Display(Name = "Has elevator")]
    public bool HasElevator { get; set; }

    [Display(Name = "Has parking")]
    public bool HasParking { get; set; }

    [Display(Name = "Furnished")]
    public bool IsFurnished { get; set; }
    public List<ApartmentPhoto> Photos { get; set; } = new List<ApartmentPhoto>();

        [NotMapped]
        public HouseProject Project => Building?.Project;
        [NotMapped]
        [Display(Name = "Real estate")]
        public HousingCompany HousingCompany => Project?.Company;

        // Assuming RealEstateName is a property in HousingCompany
        [NotMapped]
        [Display(Name = "Real estate")]
        public string RealEstateName => HousingCompany?.Name;

    }
}

