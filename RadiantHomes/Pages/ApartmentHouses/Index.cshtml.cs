using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using Microsoft.EntityFrameworkCore;

namespace RadiantHomes.Pages.ApartmentHouses
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<ApartmentHouse> ApartmentHouse { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string HouseType { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? HasBalcony { get; set; }

        // Add a new property for the Real Estate Name filter
        [BindProperty(SupportsGet = true)]
        public string RealEstateName { get; set; }

        public List<string> HouseTypes { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public int TotalPages { get; set; }

        private const int PageSize = 9;

        public async Task OnGetAsync()
        {
            var query = _context.ApartmentHouses
                .Include(h => h.HouseAddress)
                .Include(a => a.Building).ThenInclude(a=>a.Project).ThenInclude(p=>p.Company)
                .Include(a => a.Reviews)
                .Where(h => h.IsAvailable ==true && !string.IsNullOrEmpty(h.Status) && h.Status.ToLower()=="approved" ) // you can customize
                .AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                query = query.Where(h =>
                    h.Title.Contains(SearchTerm) ||
                    h.HouseAddress.City.Contains(SearchTerm) ||
                    h.HouseAddress.SubCity.Contains(SearchTerm));
            }

            if (!string.IsNullOrWhiteSpace(HouseType))
            {
                query = query.Where(h => h.HouseType == HouseTypesCateg.Residential.ToString());
            }

            if (HasBalcony.HasValue)
            {
                query = query.Where(h => h.HasBalcony == HasBalcony.Value);
            }

            // Filter by Real Estate Name (Housing Company Name)
            if (!string.IsNullOrWhiteSpace(RealEstateName))
            {
                query = query.Where(h => h.RealEstateName.Contains(RealEstateName, StringComparison.OrdinalIgnoreCase));
            }

            HouseTypes = Enum.GetNames(typeof(HouseTypesCateg)).ToList();
            
            var totalItems = query.Count();
            TotalPages =(totalItems / PageSize);

            ApartmentHouse =  query
                .OrderByDescending(h => h.CreatedOn)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }
    }
}