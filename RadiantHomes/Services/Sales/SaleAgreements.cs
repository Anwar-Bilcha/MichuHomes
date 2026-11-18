using RadiantHomes.Services.Kyc;

namespace RadiantHomes.Services.Sales
{
    /// <summary>
    /// Signed sale agreements (PDF or photo), kept outside wwwroot in App_Data/agreements/{sale id}; staff open them
    /// through Staff/Sales (permission and company checked, every opening audited).
    /// </summary>
    public sealed class SaleAgreements
    {
        private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png",
        };

        private readonly IWebHostEnvironment _env;

        public SaleAgreements(IWebHostEnvironment env) => _env = env;

        /// <summary>Null when the file is acceptable (JPG, PNG or PDF up to 10 MB whose content matches its type).</summary>
        public static string? Check(IFormFile file) => KycService.CheckFile(file) is { } problem ? $"Agreement: {problem}" : null;

        public async Task<string> SaveAsync(Guid saleId, IFormFile file)
        {
            var folder = Folder(saleId);
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.GetFiles(folder)) File.Delete(old);
            var name = "agreement" + Path.GetExtension(file.FileName).ToLowerInvariant();
            await using var stream = File.Create(Path.Combine(folder, name));
            await file.CopyToAsync(stream);
            return name;
        }

        public (string Path, string ContentType)? Find(Guid saleId, string? fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.Contains("..") || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
            var path = Path.Combine(Folder(saleId), fileName);
            if (!File.Exists(path)) return null;
            return (path, ContentTypes.GetValueOrDefault(Path.GetExtension(fileName), "application/octet-stream"));
        }

        private string Folder(Guid saleId) => Path.Combine(_env.ContentRootPath, "App_Data", "agreements", saleId.ToString("N"));
    }
}
