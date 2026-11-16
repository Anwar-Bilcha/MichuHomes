using RadiantHomes.Models.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models.HousingCompanies
{
    public class HousingCompany : BaseEntity
    {
        [Display(Name = "Company")]
        public Guid CompanyId { get; set; }

        // Basic Info
        public string Name { get; set; } = string.Empty;
        [Display(Name = "License number")]
        public string LicenseNumber { get; set; } = string.Empty;
        [Display(Name = "Established on")]
        public DateTime EstablishedDate { get; set; }

        // Address Info
        [Display(Name = "Address")]
        public Guid AddressId { get; set; }
        [ForeignKey(nameof(AddressId))]
        public PartyAddress Address { get; set; } = null!;
        public string Email { get; set; } = string.Empty;
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        [Display(Name = "Logo")]
        public string LogoUrl { get; set; } = string.Empty;
        [Display(Name = "Contact person")]
        public List<Customer> ContactPerson { get; set; } = new List<Customer>();

        // Company Metadata
        public string Status { get; set; } = "Pending";
        [Display(Name = "Company category")]
        public CompanyType CompanyCategory { get; set; } = CompanyType.Share_Company;

        // Navigation Properties
        [Display(Name = "Financial details")]
        public List<CompanyFinancialDetail> FinancialDetails { get; set; } = new List<CompanyFinancialDetail>();
        //public AppUser AdminUser { get; set; } = null!;
        public List<HouseProject> Projects { get; set; } = new List<HouseProject>();
        [Display(Name = "Customer testimonials")]
        public List<CustomerTestimonial> CustomerTestimonials { get; set; } = new List<CustomerTestimonial>();
        public List<Notification>? Notifications { get; set; }
        // Enums
        public enum CompanyType
        {
            [Display(Name = "Private limited company")]
            Private_Limitted_Company,
            [Display(Name = "Share company")]
            Share_Company,
            [Display(Name = "Sole proprietorship")]
            Sole_Proprietorship,
            [Display(Name = "Business group")]
            Buiness_Group,
            Other
        }
    }
}
