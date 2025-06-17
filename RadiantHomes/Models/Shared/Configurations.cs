namespace RadiantHomes.Models.Shared
{
    public static class Configurations
    {
        private static IWebHostEnvironment _env;

        public static void Initialize(IWebHostEnvironment env)
        {
            _env = env;
        }

        public static string ApartmentHousePhotosFolder =>
            Path.Combine(_env.WebRootPath, "uploads", "apartmenthousephotos");

        public static string HousePhotosFolder =>
            Path.Combine(_env.WebRootPath, "uploads", "houses");
    }
}
