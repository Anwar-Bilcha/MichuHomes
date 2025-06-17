using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{   
    public class ApartmentHouse : BaseEntity
    {
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string HouseNumber { get; set; } = "New";
    public string? Description { get; set; }
    public double SalePrice { get; set; }
    public List<HouseSpecs> HouseSpecs { get; set; }
    public Guid? ContactPersonId { get; set; }
    [ForeignKey(nameof(ContactPersonId))]
        [Required(ErrorMessage = "House Contact Person is required.")]
        public Customer? ContactPerson { get; set; }
    public Guid HouseCategoryId { get; set; } // Apartment, Villa, etc
    [ForeignKey(nameof(HouseCategoryId))]
    [Required(ErrorMessage = "House Category is required.")]
    public HouseCategory HouseCategory { get; set; }
    public string? AvailableFor { get; set; } // Rent or Sale
        [Required(ErrorMessage = "House Address is required.")]
        public Guid AddressId { get; set; }
        [ForeignKey(nameof(AddressId))]
        public PartyAddress HouseAddress { get; set; }
           
    public string? Status { get; set; } = "Pending"; // New, Active, Hidden, Sold, Rented etc
        public string SalesStatus { get; set; } = "None";
        public List<ApartmentHouseSaleTransaction> SaleTransactions { get; set; } = new List<ApartmentHouseSaleTransaction>();
    public List<IssueOrCompliant>? IssuesOrReportings { get; set; }
    public List<ApartmentHouseReview>? Reviews { get; set; }
    [Required(ErrorMessage = "Building for the House is required.")]
    public Guid BuildingId { get; set; }
    public string HouseType { get; set; } = HouseTypesCateg.Residential.ToString();
    public int FloorNumber { get; set; }
    [ForeignKey(nameof(BuildingId))]
    public Building? Building { get; set; }
    public int Bathrooms { get; set; }
    public int TotalRooms { get; set; }
    public double Area { get; set; }
    public bool IsAvailable { get; set; }
    public string Currency { get; set; } = "ETB";
    public int LivingRooms { get; set; }
    public int Bedrooms { get; set; }

    public bool HasBalcony { get; set; }

    public bool HasElevator { get; set; }

    public bool HasParking { get; set; }

    public bool IsFurnished { get; set; }
    public List<ApartmentPhoto> Photos { get; set; } = new List<ApartmentPhoto>();

        [NotMapped]
        public HouseProject Project => Building?.Project;
        [NotMapped]
        public HousingCompany HousingCompany => Project?.Company;

        // Assuming RealEstateName is a property in HousingCompany
        [NotMapped]
        public string RealEstateName => HousingCompany?.Name;

    }
}

