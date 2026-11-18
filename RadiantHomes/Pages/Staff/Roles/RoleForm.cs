using System.ComponentModel.DataAnnotations;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Roles
{
    /// <summary>Shared by the role pages: form fields and the permission picker (grouped by module).</summary>
    public class RoleFormInput
    {
        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;
        [StringLength(500)]
        public string? Description { get; set; }
        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
        public List<string> Permissions { get; set; } = new();
    }

    public sealed record PickerItem(string Key, string Module, string Name, string Description, bool Selected, bool Enabled);

    public static class RoleForm
    {
        /// <summary>
        /// The permissions a role of this domain may contain. Ones the editor doesn't hold are disabled (unless already in
        /// the role, so they can still be removed), except when platform staff manage a company's role (oversight).
        /// </summary>
        public static List<PickerItem> Picker(UserAccess editor, PermissionScope roleScope, IEnumerable<string> selected, bool readOnly)
        {
            var chosen = selected.ToHashSet(StringComparer.Ordinal);
            var oversight = editor.IsPlatform && roleScope == PermissionScope.Company;
            return PermissionCatalog.For(roleScope)
                .Select(p => new PickerItem(p.Key, p.Module, p.Name, p.Description, chosen.Contains(p.Key),
                    !readOnly && (oversight || editor.Has(p.Key) || chosen.Contains(p.Key))))
                .ToList();
        }

        public static string ScopeLabel(PermissionScope scope, string? companyName) =>
            scope == PermissionScope.Platform ? "Platform role (Michu Homes staff)" : $"Company role · {companyName ?? "company"}";
    }
}
