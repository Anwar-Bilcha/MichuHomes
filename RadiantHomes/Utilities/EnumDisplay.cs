using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RadiantHomes.Utilities
{
    /// <summary>
    /// Readable names for enum values in views: uses [Display(Name = …)] when present,
    /// otherwise turns "Share_Company" / "UnderConstruction" into "Share Company" / "Under Construction".
    /// Usage in Razor: @Model.Company.CompanyCategory.GetDisplayName()
    /// For &lt;select&gt;s use Html.GetEnumSelectList&lt;TEnum&gt;(), which also honours [Display].
    /// </summary>
    public static partial class EnumDisplay
    {
        public static string GetDisplayName(this Enum? value)
        {
            if (value is null) return string.Empty;

            var name = value.ToString();
            var member = value.GetType().GetField(name, BindingFlags.Public | BindingFlags.Static);
            var display = member?.GetCustomAttribute<DisplayAttribute>();
            var label = display?.GetName();
            if (!string.IsNullOrWhiteSpace(label)) return label;

            return WordBoundary().Replace(name.Replace('_', ' '), " $1").Replace("  ", " ").Trim();
        }

        [GeneratedRegex("(?<=[a-z0-9])([A-Z])")]
        private static partial Regex WordBoundary();
    }
}
