using RadiantHomes.DAL;

namespace RadiantHomes.Services
{
    public interface ILocalizationService
    {
        Dictionary<string, string> GetAllByLanguage(string language = "en");
    }
    public class LocalizationService : ILocalizationService
    {
        private readonly ApplicationDbContext _context;

        public LocalizationService(ApplicationDbContext context)
        {
            _context = context;
        }
        public Dictionary<string, string> GetAllByLanguage(string language)
        {
            return _context.Translations.ToDictionary(
                t => t.Key,
                t => language switch
                {
                    "am" => t.AmharicValue,
                    "or" => t.AfanOromoValue,
                    _ => t.EnglishValue
                } ?? t.Key
            );
        }
    }
}
