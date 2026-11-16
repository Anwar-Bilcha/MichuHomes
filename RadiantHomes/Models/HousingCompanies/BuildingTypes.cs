using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models.HousingCompanies
{
    public enum BuildingTypes
    {
        Residential,
        Office,
        [Display(Name = "Mixed use")]
        Mixed_Use,
        Commercial,
        Institutional,
        Recreational
    }
}