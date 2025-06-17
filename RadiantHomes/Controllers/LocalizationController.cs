using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RadiantHomes.Services;

namespace RadiantHomes.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocalizationController : ControllerBase
    {
        private readonly ILocalizationService _localizationService;

        public LocalizationController(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        [HttpGet("{lang}")]
        public IActionResult GetUILocalizations(string lang)
        {
            var result = _localizationService.GetAllByLanguage(lang);
            return Ok(result); // Returns Dictionary<string, string>
        }
    }
}
