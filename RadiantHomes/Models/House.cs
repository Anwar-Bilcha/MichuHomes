using RadiantHomes.Models.Shared;

namespace RadiantHomes.Models
{
    public enum RentalFrequency
    {
        Monthly,
        Quarterly,
        SemiAnnually,
        Annually
    }
    public class House :BaseEntity
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string HouseNumber { get; set; } = "New";
        public string? Description { get; set; }
        public Guid HouseOwnerId { get; set; }
        public decimal Price { get; set; }
        public int TotalBedRooms { get; set; }
        public int TotalRooms { get; set; }
        public int NumberOfBathRooms { get; set; }
        public decimal TotalArea { get; set; }
        public bool HasOwnershipDocument { get; set; }
        public string? OwnershipDocumentUrl { get; set; }
        public Guid HouseCategoryId { get; set; } // Apartment, Villa, etc
        public HouseCategory HouseCategory { get; set; }
        public string? AvailableFor { get; set; } // Rent or Sale
        public Guid AddressId { get; set; }
        public PartyAddress HouseAddress { get; set; }
        public string? HouseOwnership { get; set; } // individual, Couples, Family
        public string? ConsentofSaleOrRentDocUrl { get; set; }
        public string? LatestHouseTaxVoucherUrl { get; set; }
        public string? Status { get; set; } = "Active"; // New, Active, Hidden, Sold, Rented etc
        public Customer HouseOwner { get; set; }
        public List<HouseSaleTransaction> SaleTransactions { get; set; }
        public List<HouseRentTransaction> RentTransactions { get; set; }
        public Customer? HouseHolder { get; set; }
        public Guid HouseHolderId { get; set; }
        public string? MainPhotoUrl { get; set; }
        public string? OutOfCompoundViewPhotoUrl { get; set; }
        public string? FrontCompoundViewPhotoUrl { get; set; }
        public string? HouseInternalViewPhotoUrl { get; set; }
        public string? MainSalonViewPhotoUrl { get; set; }
        public string? MasterBedRoomViewPhotoUrl { get; set; }
        public string? KitchenViewPhotoUrl { get; set; }
        public string? RestRoomViewPhotoUrl { get; set; }
        public string? AuxiSalonViewPhotoUrl { get; set; }
        public string? SecBedRoomViewPhotoUrl { get; set; }
        public string? AdditionalViewPhotoUrl { get; set; }
        public string? BackyardViewPhotoUrl { get; set; }
        public string? Additional2PhotoUrl { get; set; }
        public List<IssueOrCompliant>? IssuesOrReportings { get; set; }
        public List<HouseReview>? Reviews { get; set; }
        public RentInfo? HouseRentInfo { get; set; }
    }
}