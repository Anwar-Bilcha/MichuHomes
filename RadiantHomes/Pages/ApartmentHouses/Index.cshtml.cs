using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.Pages.Houses;

namespace RadiantHomes.Pages.ApartmentHouses
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public IndexModel(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        /// <summary>Cover photo per apartment on this page (same photos as Details); missing → brand mark.</summary>
        public Dictionary<Guid, string> PhotoUrls { get; } = new();

        public IList<ApartmentHouse> ApartmentHouse { get; set; } = new List<ApartmentHouse>();

        // Filters are all optional query-string values: they must be nullable, otherwise model binding
        // flags them as required on every GET (red border on the empty inputs).
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? HouseType { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool? HasBalcony { get; set; }

        // Real Estate Name filter (housing company name)
        [BindProperty(SupportsGet = true)]
        public string? RealEstateName { get; set; }

        public List<string> HouseTypes { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        public int TotalPages { get; set; }
        public int TotalItems { get; set; }

        private const int PageSize = 9;

        /// <summary>Route values for the active filters, so pagination links keep them.</summary>
        public Dictionary<string, string> FilterRouteValues
        {
            get
            {
                var values = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(SearchTerm)) values[nameof(SearchTerm)] = SearchTerm.Trim();
                if (!string.IsNullOrWhiteSpace(HouseType)) values[nameof(HouseType)] = HouseType;
                if (HasBalcony.HasValue) values[nameof(HasBalcony)] = HasBalcony.Value ? "true" : "false";
                if (!string.IsNullOrWhiteSpace(RealEstateName)) values[nameof(RealEstateName)] = RealEstateName.Trim();
                return values;
            }
        }

        public async Task OnGetAsync()
        {
            HouseTypes = Enum.GetNames(typeof(HouseTypesCateg)).ToList();

            // Filter and page in SQL (SQL Server's default collation makes Contains case-insensitive).
            var query = _context.ApartmentHouses.Where(ApartmentListing.IsListed); // approved and available

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim();
                query = query.Where(h =>
                    h.Title.Contains(term) ||
                    (h.HouseAddress.City != null && h.HouseAddress.City.Contains(term)) ||
                    (h.HouseAddress.SubCity != null && h.HouseAddress.SubCity.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(HouseType))
            {
                query = query.Where(h => h.HouseType == HouseType);
            }

            if (HasBalcony.HasValue)
            {
                query = query.Where(h => h.HasBalcony == HasBalcony.Value);
            }

            // Filter by Real Estate Name (Housing Company Name)
            if (!string.IsNullOrWhiteSpace(RealEstateName))
            {
                var company = RealEstateName.Trim();
                query = query.Where(h => h.Building != null && h.Building.Project != null && h.Building.Project.Company != null
                                         && h.Building.Project.Company.Name.Contains(company));
            }

            TotalItems = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalItems / (double)PageSize);
            PageNumber = Math.Clamp(PageNumber, 1, Math.Max(TotalPages, 1));

            ApartmentHouse = await query
                .Include(h => h.HouseAddress)
                .Include(a => a.Building).ThenInclude(a => a.Project).ThenInclude(p => p.Company)
                .Include(a => a.Reviews)
                .OrderByDescending(h => h.CreatedOn)
                .ThenBy(h => h.Id)
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .AsSplitQuery()
                .ToListAsync();

            // One directory listing per distinct photo folder for the whole page (ApartmentCoverPhotos caches them).
            var photos = new ApartmentCoverPhotos(_env);
            foreach (var house in ApartmentHouse)
            {
                var url = photos.Find(house.Id, house.Building?.Project?.Company?.Name, house.Bedrooms);
                if (url is not null) PhotoUrls[house.Id] = url;
            }
            With3DModel = ApartmentModel3D.WithModels(_env, ApartmentHouse.Select(h => h.Id));
        }

        /// <summary>Apartments on this page that have a 3D model (a "3D view" badge on their card).</summary>
        public HashSet<Guid> With3DModel { get; private set; } = new();
    }
}