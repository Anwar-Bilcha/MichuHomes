using DocumentFormat.OpenXml.ExtendedProperties;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models
{
    //MichuAdmin => 
    //RealEstateAdmin
    //Customer
    public class Customer : BaseEntity
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public Guid RankingId { get; set; }
        [ForeignKey(nameof(RankingId))]
        public HomeOwnerRanking? Ranking { get; set; }
        public string? CustomerCategory { get; set; } // Individual, Agency , Real Estate
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public Guid AddressId { get; set; }
        public string CustomerRole { get; set; } = CustomerRoles.MultiRole.ToString();
        [ForeignKey(nameof(AddressId))]
        public PartyAddress? Address { get; set; }
        public string? DocumentUsed { get; set; } //NationalID, Passport, KebeleId, EmployeeID, StudentId, Other
        public string? IdCardFileUrl { get; set; }

        public string? CustomerPhotoUrl { get; set; }
        public DateTime ApplicationDate { get; set; } = DateTime.Now;
        public DateTime? ApprovedDate { get; set; }
        public string ProfileStatus { get; set; } = "Pending"; // Pending, Approved, Rejected

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
        RealEstateAdmin,
        Consultant,
        MultiRole
    }
}
