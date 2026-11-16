using System.Text.RegularExpressions;

namespace RadiantHomes.Utilities
{
    /// <summary>
    /// Helpers for saving user uploads safely: folder names and file names are never taken
    /// verbatim from user input (no "..", slashes or odd characters), and only known file types are accepted.
    /// </summary>
    public static partial class SafeUpload
    {
        public const long MaxBytes = 10 * 1024 * 1024; // 10 MB

        public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        public static readonly string[] DocumentExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

        /// <summary>A single, safe path segment: letters, digits, '-' and '_' only.</summary>
        public static string Segment(string? value, string fallback = "default")
        {
            var cleaned = UnsafeChars().Replace(value ?? string.Empty, "_").Trim('_', '.', ' ');
            return string.IsNullOrEmpty(cleaned) ? fallback : cleaned;
        }

        /// <summary>Lower-case extension of the upload, or empty if it isn't in the allowed list.</summary>
        public static string AllowedExtension(IFormFile file, IEnumerable<string> allowed)
        {
            var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
            return allowed.Contains(ext) ? ext : string.Empty;
        }

        /// <summary>Returns an error message, or null if the file is acceptable.</summary>
        public static string? Validate(IFormFile file, IEnumerable<string> allowed)
        {
            if (file.Length <= 0) return $"\"{file.FileName}\" is empty.";
            if (file.Length > MaxBytes) return $"\"{file.FileName}\" is larger than {MaxBytes / (1024 * 1024)} MB.";
            if (AllowedExtension(file, allowed).Length == 0)
                return $"\"{file.FileName}\" is not an allowed file type ({string.Join(", ", allowed)}).";
            return null;
        }

        /// <summary>Unique, safe file name that keeps a readable part of the original name.</summary>
        public static string FileName(IFormFile file, IEnumerable<string> allowed)
        {
            var baseName = Segment(Path.GetFileNameWithoutExtension(file.FileName), "file");
            if (baseName.Length > 60) baseName = baseName[..60];
            return $"{baseName}_{Guid.NewGuid():N}{AllowedExtension(file, allowed)}";
        }

        /// <summary>Combines segments under a root and guarantees the result stays inside the root.</summary>
        public static string Under(string root, params string[] segments)
        {
            var rootFull = Path.GetFullPath(root);
            var path = Path.GetFullPath(Path.Combine(new[] { rootFull }.Concat(segments.Select(s => Segment(s))).ToArray()));
            if (!path.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Upload path escapes its root folder.");
            return path;
        }

        [GeneratedRegex(@"[^A-Za-z0-9_\-]+")]
        private static partial Regex UnsafeChars();
    }
}
