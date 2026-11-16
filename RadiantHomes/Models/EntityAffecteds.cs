using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public enum EntityAffecteds
    {
        [Display(Name = "Apartment houses")]
        ApartmentHouses,
        [Display(Name = "User profiles")]
        UserProfiles,
        Houses,
        [Display(Name = "House reviews")]
        HouseReviews,
        [Display(Name = "Issues and complaints")]
        IssueOrCompliants,
        Addresses,
        [Display(Name = "User accounts")]
        AppUser,
        [Display(Name = "Apartment house reviews")]
        ApartmentHouseReviews,
        [Display(Name = "Apartment house sale transactions")]
        ApartmentHouseSaleTransactions,
        [Display(Name = "Apartment photos")]
        ApartmentPhotos,
        Buildings,
        [Display(Name = "Company financial details")]
        CompanyFinancialDetails,
        [Display(Name = "Customer testimonials")]
        CustomerTestinomials,
        [Display(Name = "House projects")]
        HouseProjects,
        [Display(Name = "House specifications")]
        HouseSpecs,
        [Display(Name = "Housing companies")]
        HousingCompanies
    }
}