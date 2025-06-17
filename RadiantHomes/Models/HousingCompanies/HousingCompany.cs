using RadiantHomes.Models.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class HousingCompany : BaseEntity
    {
        public Guid CompanyId { get; set; }

        // Basic Info
        public string Name { get; set; } = string.Empty;
        public string LicenseNumber { get; set; } = string.Empty;
        public DateTime EstablishedDate { get; set; }

        // Address Info
        public Guid AddressId { get; set; }
        [ForeignKey(nameof(AddressId))]
        public PartyAddress Address { get; set; } = null!;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public List<Customer> ContactPerson { get; set; } = new List<Customer>();

        // Company Metadata
        public string Status { get; set; } = "Pending";
        public CompanyType CompanyCategory { get; set; } = CompanyType.Share_Company;

        // Navigation Properties
        public List<CompanyFinancialDetail> FinancialDetails { get; set; } = new List<CompanyFinancialDetail>();
        //public AppUser AdminUser { get; set; } = null!;
        public List<HouseProject> Projects { get; set; } = new List<HouseProject>();
        public List<CustomerTestimonial> CustomerTestimonials { get; set; } = new List<CustomerTestimonial>();
        public List<Notification>? Notifications { get; set; }
        // Enums
        public enum CompanyType
        {
            Private_Limitted_Company,
            Share_Company,
            Sole_Proprietorship,
            Buiness_Group,
            Other
        }
    }
}
