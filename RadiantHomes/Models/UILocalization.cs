using RadiantHomes.Models.Shared;

namespace RadiantHomes.Models
{
    public class UILocalization :BaseEntity
    {
        public int ID { get; set; }
        public string Key { get; set; }
        public string AmharicValue { get; set; }
        public string EnglishValue { get; set; }
        public string AfanOromoValue { get; set; }
        public string? AdditionalNote { get; set; }
        public string? Page { get; set; }

    }
}
