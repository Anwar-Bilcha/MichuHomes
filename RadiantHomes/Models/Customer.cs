using DocumentFormat.OpenXml.ExtendedProperties;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    //MichuAdmin => 
    //RealEstateAdmin
    //Customer
    public class Customer : BaseEntity
    {
        public Guid Id { get; set; }
        [Display(Name = "First name")]
        public string FirstName { get; set; }
        [Display(Name = "Last name")]
        public string LastName { get; set; }
        [Display(Name = "Ranking")]
        public Guid RankingId { get; set; }
        [ForeignKey(nameof(RankingId))]
        public HomeOwnerRanking? Ranking { get; set; }
        [Display(Name = "Customer category")]
        public string? CustomerCategory { get; set; } // Individual, Agency , Real Estate
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        [Display(Name = "Address")]
        public Guid AddressId { get; set; }
        [Display(Name = "Customer role")]
        public string CustomerRole { get; set; } = CustomerRoles.MultiRole.ToString();
        [ForeignKey(nameof(AddressId))]
        public PartyAddress? Address { get; set; }
        [Display(Name = "Document used")]
        public string? DocumentUsed { get; set; } //NationalID, Passport, KebeleId, EmployeeID, StudentId, Other
        [Display(Name = "ID card")]
        public string? IdCardFileUrl { get; set; }

        [Display(Name = "Profile photo")]
        public string? CustomerPhotoUrl { get; set; }
        [Display(Name = "Application date")]
        public DateTime ApplicationDate { get; set; } = DateTime.Now;
        [Display(Name = "Approval date")]
        public DateTime? ApprovedDate { get; set; }
        [Display(Name = "Profile status")]
        public string ProfileStatus { get; set; } = "Pending"; // Pending, Approved, Rejected
        /// <summary>Identity verification (Models/Kyc): NotSubmitted, Pending, UnderReview, Approved, Rejected, ResubmissionRequired, Revoked.</summary>
        [System.ComponentModel.DataAnnotations.MaxLength(30)]
        public string KycStatus { get; set; } = RadiantHomes.Models.Kyc.KycStatuses.NotSubmitted;

        [Display(Name = "User account")]
        public AppUser CustomerUserAccount { get; set; }
        public List<House>? Houses { get; set; }
        public List<HouseSaleTransaction>? SaleTransactions { get; set; }
        public List<HouseSaleTransaction>? PurchaseTransactions { get; set; }
        public List<ApartmentHouseSaleTransaction>? ApartmentSaleTransactions { get; set; }
        public List<ApartmentHouseSaleTransaction>? ApartmentPurchaseTransactions { get; set; }

        public List<HouseRentTransaction>? RentTransactions { get; set; }
        public List<HouseRentTransaction>? HouseRentTransactions { get; set; }
        public List<CustomerTestimonial> Testimonials { get; set; }
        public List<HouseReview> Reviews { get; set; }
        public List<ApartmentHouseReview> ApartmentReviews { get; set; }
        public List<ApartmentHouse>? ApartmentHouses { get; set; }
        public List<Notification>? Notifications { get; set; }
        [Display(Name = "Company")]
        public Guid? CompanyId { get; set; }
        [ForeignKey(nameof(CompanyId))]

        public HousingCompany? Company { get; set; }
    }

    public enum CustomerRoles
    {
        Buyer,
        Seller,
        Tenant,
        Broker,
        [Display(Name = "Real estate admin")]
        RealEstateAdmin,
        Consultant,
        [Display(Name = "Multiple roles")]
        MultiRole
    }
}
