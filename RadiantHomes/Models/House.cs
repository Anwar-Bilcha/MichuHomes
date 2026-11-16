using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public enum RentalFrequency
    {
        Monthly,
        Quarterly,
        [Display(Name = "Semi-annually")]
        SemiAnnually,
        Annually
    }
    public class House :BaseEntity
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        [Display(Name = "House number")]
        public string HouseNumber { get; set; } = "New";
        public string? Description { get; set; }
        [Display(Name = "Owner")]
        public Guid HouseOwnerId { get; set; }
        public decimal Price { get; set; }
        [Display(Name = "Bedrooms")]
        public int TotalBedRooms { get; set; }
        [Display(Name = "Total rooms")]
        public int TotalRooms { get; set; }
        [Display(Name = "Bathrooms")]
        public int NumberOfBathRooms { get; set; }
        [Display(Name = "Total area")]
        public decimal TotalArea { get; set; }
        [Display(Name = "Has ownership document")]
        public bool HasOwnershipDocument { get; set; }
        [Display(Name = "Ownership document")]
        public string? OwnershipDocumentUrl { get; set; }
        [Display(Name = "Category")]
        public Guid HouseCategoryId { get; set; } // Apartment, Villa, etc
        [Display(Name = "Category")]
        public HouseCategory HouseCategory { get; set; }
        [Display(Name = "Available for")]
        public string? AvailableFor { get; set; } // Rent or Sale
        [Display(Name = "Address")]
        public Guid AddressId { get; set; }
        [Display(Name = "Address")]
        public PartyAddress HouseAddress { get; set; }
        [Display(Name = "Ownership")]
        public string? HouseOwnership { get; set; } // individual, Couples, Family
        [Display(Name = "Consent of sale or rent")]
        public string? ConsentofSaleOrRentDocUrl { get; set; }
        [Display(Name = "Latest tax voucher")]
        public string? LatestHouseTaxVoucherUrl { get; set; }
        public string? Status { get; set; } = "Active"; // New, Active, Hidden, Sold, Rented etc
        [Display(Name = "Owner")]
        public Customer HouseOwner { get; set; }
        public List<HouseSaleTransaction> SaleTransactions { get; set; }
        public List<HouseRentTransaction> RentTransactions { get; set; }
        [Display(Name = "Householder")]
        public Customer? HouseHolder { get; set; }
        [Display(Name = "Householder")]
        public Guid HouseHolderId { get; set; }
        [Display(Name = "Main photo")]
        public string? MainPhotoUrl { get; set; }
        [Display(Name = "Outside compound photo")]
        public string? OutOfCompoundViewPhotoUrl { get; set; }
        [Display(Name = "Front compound photo")]
        public string? FrontCompoundViewPhotoUrl { get; set; }
        [Display(Name = "Interior photo")]
        public string? HouseInternalViewPhotoUrl { get; set; }
        [Display(Name = "Main salon photo")]
        public string? MainSalonViewPhotoUrl { get; set; }
        [Display(Name = "Master bedroom photo")]
        public string? MasterBedRoomViewPhotoUrl { get; set; }
        [Display(Name = "Kitchen photo")]
        public string? KitchenViewPhotoUrl { get; set; }
        [Display(Name = "Restroom photo")]
        public string? RestRoomViewPhotoUrl { get; set; }
        [Display(Name = "Auxiliary salon photo")]
        public string? AuxiSalonViewPhotoUrl { get; set; }
        [Display(Name = "Second bedroom photo")]
        public string? SecBedRoomViewPhotoUrl { get; set; }
        [Display(Name = "Additional photo")]
        public string? AdditionalViewPhotoUrl { get; set; }
        [Display(Name = "Backyard photo")]
        public string? BackyardViewPhotoUrl { get; set; }
        [Display(Name = "Additional photo 2")]
        public string? Additional2PhotoUrl { get; set; }
        public List<IssueOrCompliant>? IssuesOrReportings { get; set; }
        public List<HouseReview>? Reviews { get; set; }
        [Display(Name = "Rent details")]
        public RentInfo? HouseRentInfo { get; set; }
    }
}