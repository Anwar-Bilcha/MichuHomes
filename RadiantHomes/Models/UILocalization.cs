using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public class UILocalization :BaseEntity
    {
        public int ID { get; set; }
        public string Key { get; set; }
        [Display(Name = "Amharic")]
        public string AmharicValue { get; set; }
        [Display(Name = "English")]
        public string EnglishValue { get; set; }
        [Display(Name = "Additional note")]
        public string? AdditionalNote { get; set; }
        public string? Page { get; set; }

    }
}
