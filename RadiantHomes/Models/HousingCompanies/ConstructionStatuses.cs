using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models.HousingCompanies
{
    public enum ConstructionStatuses
    {
        Planned,
        [Display(Name = "Foundation done")]
        Foundation_Done,
        [Display(Name = "Quarter completed")]
        Quarter_Percent_Completed,
        [Display(Name = "Half completed")]
        Half_Completed,
        [Display(Name = "Three-quarters completed")]
        Three_Quarter_Completed,
        Completed,
        [Display(Name = "Under finishing")]
        Under_Finishing

    }
}