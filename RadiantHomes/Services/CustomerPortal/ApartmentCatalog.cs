using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Shared;
using RadiantHomes.Pages.ApartmentHouses;
using RadiantHomes.Pages.Houses;

namespace RadiantHomes.Services.CustomerPortal
{
    /// <summary>What an apartment card in the portal shows.</summary>
    public sealed record ApartmentCard(
        Guid Id, string Title, double Price, string Currency, string? PhotoUrl,
        string? Site, string? Area, string? Company, int Bedrooms, int Bathrooms, double SquareMetres, bool Has3D)
    {
        public bool HasPrice => Price > 0;
        public string PriceText => ApartmentHouseForm.Price(Price, Currency);
    }

    /// <summary>Filters of Portal/Browse (all optional query-string values).</summary>
    public sealed class ListingFilter
    {
        public Guid? Site { get; set; }
        public string? Area { get; set; }
        /// <summary>Bedrooms; <see cref="ApartmentCatalog.MaxBedroomFilter"/> means "that many or more".</summary>
        public int? Bedrooms { get; set; }
        public Guid? Company { get; set; }
        public double? MaxPrice { get; set; }
        public string? Q { get; set; }
        /// <summary>"newest" (default), "price" (lowest first) or "price-desc".</summary>
        public string? Sort { get; set; }

        public bool Any => Site != null || !string.IsNullOrWhiteSpace(Area) || Bedrooms != null || Company != null
                           || MaxPrice is > 0 || !string.IsNullOrWhiteSpace(Q);
    }

    /// <summary>A group of "browse by": a site, a number of bedrooms or a real estate company.</summary>
    public sealed record ListingGroup(string Key, string Name, string? Detail, int Count, double? FromPrice, string Currency, string? PhotoUrl)
    {
        public string? FromPriceText => FromPrice is double price ? ApartmentHouseForm.Price(price, Currency) : null;
    }

    /// <summary>A company's lowest price (for a number of bedrooms) in the "Best prices" comparison.</summary>
    public sealed record CompanyPrice(
        Guid CompanyId, string Company, string? LogoUrl, double LowestPrice, string Currency, int Count,
        Guid CheapestId, string CheapestTitle, bool IsBest)
    {
        public string LowestPriceText => ApartmentHouseForm.Price(LowestPrice, Currency);
    }

    /// <summary>How an apartment's price compares with the lowest one for the same number of bedrooms.</summary>
    public sealed record PriceCheck(int Companies, CompanyPrice Best, bool IsBest, double Difference)
    {
        public string DifferenceText => ApartmentHouseForm.Price(Difference, Best.Currency);
    }

    /// <summary>
    /// The listed apartments (approved and available, see ApartmentListing.IsListed) as the customer portal shows
    /// them: search and filters, "browse by" groups, price comparison across real estates and similar homes.
    /// A "site" is a housing project (its name and location).
    /// </summary>
    public sealed class ApartmentCatalog
    {
        public const int MaxBedroomFilter = 4;

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ApartmentCatalog(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public IQueryable<ApartmentHouse> Listed() => WithDetails(_context.ApartmentHouses.AsNoTracking().Where(ApartmentListing.IsListed));

        public static IQueryable<ApartmentHouse> WithDetails(IQueryable<ApartmentHouse> query) => query
            .Include(a => a.HouseAddress)
            .Include(a => a.Building!).ThenInclude(b => b.Project!).ThenInclude(p => p.Company);

        // ---------- search ----------

        public async Task<(List<ApartmentCard> Items, int Total)> SearchAsync(ListingFilter filter, int page, int pageSize)
        {
            var query = Listed();
            if (filter.Site is Guid site) query = query.Where(a => a.Building!.ProjectId == site);
            if (!string.IsNullOrWhiteSpace(filter.Area))
            {
                var area = filter.Area.Trim();
                query = query.Where(a => a.HouseAddress.SubCity == area);
            }
            if (filter.Bedrooms is int beds) query = WithBedrooms(query, beds);
            if (filter.Company is Guid company) query = query.Where(a => a.Building!.Project!.HousingCompanyId == company);
            if (filter.MaxPrice is double max && max > 0) query = query.Where(a => a.SalePrice > 0 && a.SalePrice <= max);
            if (!string.IsNullOrWhiteSpace(filter.Q))
            {
                var q = filter.Q.Trim();
                query = query.Where(a => a.Title.Contains(q)
                                         || (a.Description != null && a.Description.Contains(q))
                                         || a.Building!.Project!.ProjectName.Contains(q)
                                         || (a.HouseAddress.SubCity != null && a.HouseAddress.SubCity.Contains(q))
                                         || a.HouseAddress.City.Contains(q));
            }
            query = filter.Sort switch
            {
                "price" => query.OrderBy(a => a.SalePrice <= 0).ThenBy(a => a.SalePrice), // "price not listed" last
                "price-desc" => query.OrderByDescending(a => a.SalePrice),
                _ => query.OrderByDescending(a => a.CreatedOn),
            };

            var total = await query.CountAsync();
            var items = await query.Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToListAsync();
            return (ToCards(items), total);
        }

        public async Task<List<ApartmentCard>> LatestAsync(int take) =>
            ToCards(await Listed().OrderByDescending(a => a.CreatedOn).Take(take).ToListAsync());

        /// <summary>Areas (sub-cities) that have listed apartments, for the filter.</summary>
        public async Task<List<string>> AreasAsync() =>
            (await Listed().Select(a => a.HouseAddress.SubCity).Where(s => s != null && s != "").Distinct().ToListAsync())
                .Select(s => s!).OrderBy(s => s).ToList();

        // ---------- browse by ----------

        public async Task<List<ListingGroup>> SitesAsync()
        {
            var all = await Listed().ToListAsync();
            var covers = new ApartmentCoverPhotos(_env);
            return all.Where(a => a.Building?.Project != null)
                .GroupBy(a => a.Building!.ProjectId)
                .Select(g =>
                {
                    var first = g.First();
                    var project = first.Building!.Project!;
                    var where = !string.IsNullOrWhiteSpace(project.Location) ? project.Location : first.HouseAddress?.SubCity ?? first.HouseAddress?.City;
                    var detail = string.Join(" · ", new[] { where, project.Company?.Name }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    var cheapest = Cheapest(g);
                    return new ListingGroup(g.Key.ToString(), project.ProjectName, detail, g.Count(), cheapest?.SalePrice,
                        ApartmentHouseForm.CurrencyOrDefault(cheapest?.Currency), Cover(covers, first));
                })
                .OrderBy(g => g.Name)
                .ToList();
        }

        /// <summary>Groups keyed "0" (studio), "1", "2", "3" and "4" (4 or more bedrooms).</summary>
        public async Task<List<ListingGroup>> BedroomGroupsAsync()
        {
            var all = await Listed().ToListAsync();
            var covers = new ApartmentCoverPhotos(_env);
            return all.GroupBy(a => Math.Min(Math.Max(a.Bedrooms, 0), MaxBedroomFilter))
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var cheapest = Cheapest(g);
                    return new ListingGroup(g.Key.ToString(), BedroomsLabel(g.Key), null, g.Count(), cheapest?.SalePrice,
                        ApartmentHouseForm.CurrencyOrDefault(cheapest?.Currency), Cover(covers, g.First()));
                })
                .ToList();
        }

        public async Task<List<ListingGroup>> CompaniesAsync()
        {
            var all = await Listed().ToListAsync();
            return all.Where(a => a.Building?.Project?.Company != null)
                .GroupBy(a => a.Building!.Project!.HousingCompanyId)
                .Select(g =>
                {
                    var company = g.First().Building!.Project!.Company!;
                    var sites = g.Select(a => a.Building!.ProjectId).Distinct().Count();
                    var cheapest = Cheapest(g);
                    return new ListingGroup(g.Key.ToString(), company.Name, sites == 1 ? "1 site" : $"{sites} sites", g.Count(),
                        cheapest?.SalePrice, ApartmentHouseForm.CurrencyOrDefault(cheapest?.Currency),
                        string.IsNullOrWhiteSpace(company.LogoUrl) ? null : company.LogoUrl);
                })
                .OrderBy(g => g.Name)
                .ToList();
        }

        public static string BedroomsLabel(int bedrooms) => bedrooms switch
        {
            <= 0 => "Studio",
            1 => "1 bedroom",
            >= MaxBedroomFilter => $"{MaxBedroomFilter}+ bedrooms",
            _ => $"{bedrooms} bedrooms",
        };

        // ---------- prices ----------

        /// <summary>
        /// Each company's lowest asking price (optionally for a number of bedrooms), cheapest first. Prices are
        /// compared within a currency: the lowest of each currency is marked best.
        /// </summary>
        public async Task<List<CompanyPrice>> LowestPricesAsync(int? bedrooms)
        {
            var query = Listed().Where(a => a.SalePrice > 0);
            if (bedrooms is int beds) query = WithBedrooms(query, beds);
            var all = await query.ToListAsync();

            var rows = all.Where(a => a.Building?.Project?.Company != null)
                .GroupBy(a => (a.Building!.Project!.HousingCompanyId, Currency: ApartmentHouseForm.CurrencyOrDefault(a.Currency)))
                .Select(g =>
                {
                    var cheapest = g.OrderBy(a => a.SalePrice).First();
                    var company = cheapest.Building!.Project!.Company!;
                    return new CompanyPrice(company.CompanyId, company.Name, string.IsNullOrWhiteSpace(company.LogoUrl) ? null : company.LogoUrl,
                        cheapest.SalePrice, g.Key.Currency, g.Count(), cheapest.Id, cheapest.Title, false);
                })
                .ToList();

            var best = rows.GroupBy(r => r.Currency).ToDictionary(g => g.Key, g => g.Min(r => r.LowestPrice));
            return rows.Select(r => r with { IsBest = r.LowestPrice == best[r.Currency] })
                .OrderBy(r => r.Currency != ApartmentHouseForm.DefaultCurrency)
                .ThenBy(r => r.Currency)
                .ThenBy(r => r.LowestPrice)
                .ToList();
        }

        /// <summary>Null when the apartment has no price or nothing comparable is listed.</summary>
        public async Task<PriceCheck?> PriceCheckAsync(ApartmentHouse apartment)
        {
            if (apartment.SalePrice <= 0) return null;
            var currency = ApartmentHouseForm.CurrencyOrDefault(apartment.Currency);
            var rows = (await LowestPricesAsync(apartment.Bedrooms)).Where(r => r.Currency == currency).ToList();
            var best = rows.FirstOrDefault(r => r.IsBest);
            if (best is null) return null;
            var isBest = apartment.SalePrice <= best.LowestPrice;
            return new PriceCheck(rows.Count, best, isBest, Math.Max(0, apartment.SalePrice - best.LowestPrice));
        }

        // ---------- one apartment ----------

        /// <summary>The apartment with what the portal page shows, whether or not it is listed (reserved, bought…).</summary>
        public Task<ApartmentHouse?> FindAsync(Guid id) =>
            WithDetails(_context.ApartmentHouses.AsNoTracking()).FirstOrDefaultAsync(a => a.Id == id);

        /// <summary>Same number of bedrooms; the same site first, then the closest price.</summary>
        public async Task<List<ApartmentCard>> SimilarAsync(ApartmentHouse apartment, int take = 8)
        {
            var candidates = await Listed().Where(a => a.Id != apartment.Id && a.Bedrooms == apartment.Bedrooms).ToListAsync();
            var siteId = apartment.Building?.ProjectId;
            var ranked = candidates
                .OrderBy(a => a.Building?.ProjectId == siteId ? 0 : 1)
                .ThenBy(a => apartment.SalePrice > 0 && a.SalePrice > 0 ? Math.Abs(a.SalePrice - apartment.SalePrice) : double.MaxValue)
                .Take(take);
            return ToCards(ranked);
        }

        /// <summary>
        /// Photos of the apartment page: its own uploads first, then the company's shared photos for apartments with
        /// the same number of bedrooms (the same rule as ApartmentHouses/Details).
        /// </summary>
        public List<string> Photos(ApartmentHouse apartment)
        {
            Configurations.Initialize(_env);
            var companyFolder = (apartment.Building?.Project?.Company?.Name ?? "default").Split(' ')[0];
            var folders = new[]
            {
                Path.Combine(_env.WebRootPath, "uploads", "apartmentphotos", apartment.Id.ToString()),
                Path.Combine(Configurations.ApartmentHousePhotosFolder, companyFolder, $"{apartment.Bedrooms}bed"),
            };
            var photos = new List<string>();
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                photos.AddRange(Directory.GetFiles(folder)
                    .Where(f => SafeUploadImage(f))
                    .OrderBy(f => File.GetCreationTimeUtc(f))
                    .Select(f => "/" + Path.GetRelativePath(_env.WebRootPath, f).Replace("\\", "/")));
            }
            return photos;
        }

        public List<ApartmentCard> ToCards(IEnumerable<ApartmentHouse> apartments)
        {
            var list = apartments.ToList();
            var covers = new ApartmentCoverPhotos(_env);
            var with3D = ApartmentModel3D.WithModels(_env, list.Select(a => a.Id));
            return list.Select(a => new ApartmentCard(
                a.Id, a.Title, a.SalePrice, ApartmentHouseForm.CurrencyOrDefault(a.Currency), Cover(covers, a),
                a.Building?.Project?.ProjectName, a.HouseAddress?.SubCity ?? a.HouseAddress?.City, a.Building?.Project?.Company?.Name,
                a.Bedrooms, a.Bathrooms, a.Area, with3D.Contains(a.Id))).ToList();
        }

        // ---------- helpers ----------

        private static IQueryable<ApartmentHouse> WithBedrooms(IQueryable<ApartmentHouse> query, int bedrooms) =>
            bedrooms >= MaxBedroomFilter ? query.Where(a => a.Bedrooms >= MaxBedroomFilter) : query.Where(a => a.Bedrooms == bedrooms);

        /// <summary>Cheapest priced apartment of a group, preferring the default currency (ETB).</summary>
        private static ApartmentHouse? Cheapest(IEnumerable<ApartmentHouse> group)
        {
            var priced = group.Where(a => a.SalePrice > 0).ToList();
            var inDefault = priced.Where(a => ApartmentHouseForm.CurrencyOrDefault(a.Currency) == ApartmentHouseForm.DefaultCurrency).ToList();
            return (inDefault.Count > 0 ? inDefault : priced).OrderBy(a => a.SalePrice).FirstOrDefault();
        }

        private static string? Cover(ApartmentCoverPhotos covers, ApartmentHouse apartment) =>
            covers.Find(apartment.Id, apartment.Building?.Project?.Company?.Name, apartment.Bedrooms);

        private static bool SafeUploadImage(string path) =>
            RadiantHomes.Utilities.SafeUpload.ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
    }
}
