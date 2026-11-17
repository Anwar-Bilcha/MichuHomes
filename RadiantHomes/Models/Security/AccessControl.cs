using System.ComponentModel.DataAnnotations;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Shared;

namespace RadiantHomes.Models.Security
{
    // Permission-based access control (RBAC): user → role(s) → permissions.
    // Two independent administrative domains: platform roles (Michu Homes staff, across all companies) and company
    // roles (each real estate company's own staff, within that company only). Customers are not staff and hold no roles.
    // The permission catalog lives in code (Services/Security/PermissionCatalog.cs) and is synced into Permissions.

    /// <summary>Which kind of account this is. Staff accounts belong to exactly one administrative domain.</summary>
    public enum AccountType
    {
        Customer = 0,
        PlatformStaff = 1,
        CompanyStaff = 2,
    }

    /// <summary>Which domain may hold a permission or own a role.</summary>
    public enum PermissionScope
    {
        Platform = 1,
        Company = 2,
        Both = 3,
    }

    /// <summary>A permission from the catalog (key such as "projects.create").</summary>
    public class Permission
    {
        [Key, MaxLength(100)]
        public string Key { get; set; } = string.Empty;
        [MaxLength(100)]
        public string Module { get; set; } = string.Empty;
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
        public PermissionScope Scope { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// A named set of permissions. Platform roles have no company; company roles belong to one company and can only
    /// be given to that company's staff. Built-in (system) roles can't be renamed, edited or deleted.
    /// </summary>
    public class AccessRole : BaseEntity
    {
        public Guid Id { get; set; }
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(500)]
        public string? Description { get; set; }
        /// <summary>Platform or Company (never Both).</summary>
        public PermissionScope Scope { get; set; }
        public Guid? CompanyId { get; set; }
        public HousingCompany? Company { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; } = true;
        public List<AccessRolePermission> Permissions { get; set; } = new();
        public List<UserAccessRole> Users { get; set; } = new();
    }

    public class AccessRolePermission
    {
        public Guid RoleId { get; set; }
        public AccessRole Role { get; set; } = null!;
        [MaxLength(100)]
        public string PermissionKey { get; set; } = string.Empty;
        public Permission Permission { get; set; } = null!;
    }

    public class UserAccessRole
    {
        public string UserId { get; set; } = string.Empty;
        public AppUser User { get; set; } = null!;
        public Guid RoleId { get; set; }
        public AccessRole Role { get; set; } = null!;
        [MaxLength(256)]
        public string? AssignedBy { get; set; }
        public DateTime AssignedOn { get; set; }
    }

    /// <summary>Who did what, when (role, permission, user, company status changes and other critical actions).</summary>
    public class AuditLog
    {
        public long Id { get; set; }
        public DateTime OccurredOn { get; set; }
        [MaxLength(450)]
        public string? ActorUserId { get; set; }
        [MaxLength(256)]
        public string? ActorName { get; set; }
        /// <summary>The company the change belongs to (null for platform-level changes). Company staff see their own.</summary>
        public Guid? CompanyId { get; set; }
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty;
        [MaxLength(80)]
        public string Action { get; set; } = string.Empty;
        [MaxLength(80)]
        public string? EntityType { get; set; }
        [MaxLength(100)]
        public string? EntityId { get; set; }
        [MaxLength(1000)]
        public string Summary { get; set; } = string.Empty;
        /// <summary>Optional JSON with before/after values.</summary>
        public string? Details { get; set; }
        [MaxLength(64)]
        public string? IpAddress { get; set; }
    }

    /// <summary>Every change of a company's approval status, with the reason given.</summary>
    public class CompanyStatusChange
    {
        public long Id { get; set; }
        public Guid CompanyId { get; set; }
        public HousingCompany Company { get; set; } = null!;
        [MaxLength(30)]
        public string? FromStatus { get; set; }
        [MaxLength(30)]
        public string ToStatus { get; set; } = string.Empty;
        [MaxLength(1000)]
        public string? Reason { get; set; }
        [MaxLength(450)]
        public string? ChangedByUserId { get; set; }
        [MaxLength(256)]
        public string? ChangedByName { get; set; }
        public DateTime ChangedOn { get; set; }
    }

    /// <summary>Company approval statuses (stored as text in HousingCompanies.Status).</summary>
    public static class CompanyStatuses
    {
        /// <summary>Waiting for the platform's review.</summary>
        public const string Pending = "Pending";
        /// <summary>The platform asked the company for more information; the company resubmits.</summary>
        public const string InfoRequested = "InfoRequested";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Suspended = "Suspended";

        public static readonly string[] All = { Pending, InfoRequested, Approved, Rejected, Suspended };

        public static string Label(string? status) => status switch
        {
            Pending => "Waiting for approval",
            InfoRequested => "More information requested",
            Approved => "Approved",
            Rejected => "Rejected",
            Suspended => "Suspended",
            null or "" => "Unknown",
            _ => status,
        };

        public static string Badge(string? status) => status switch
        {
            Approved => "text-bg-success",
            Pending => "text-bg-warning",
            InfoRequested => "text-bg-info",
            Rejected => "text-bg-danger",
            Suspended => "text-bg-secondary",
            _ => "text-bg-light",
        };
    }
}
