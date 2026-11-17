using RadiantHomes.Models.Security;

namespace RadiantHomes.Services.Security
{
    /// <summary>
    /// Permission keys ("module.resource.action"). Pages, handlers, menus and buttons check these — never role names.
    /// A key held through a platform role applies to every company; through a company role, to that company only.
    /// </summary>
    public static class Perm
    {
        // Platform administration (platform roles only)
        public const string PlatformDashboardView = "platform.dashboard.view";
        public const string CompaniesView = "companies.view";
        public const string CompaniesCreate = "companies.create";
        public const string CompaniesUpdate = "companies.update";
        public const string CompaniesApprove = "companies.approve";
        public const string CompaniesSuspend = "companies.suspend";
        public const string CompaniesDelete = "companies.delete";
        public const string ReportsView = "reports.view";
        public const string HousesApprove = "houses.approve";
        public const string OwnerHousesManage = "owner_houses.manage";
        public const string KycView = "kyc.view";
        public const string KycReview = "kyc.review";
        public const string KycRevoke = "kyc.revoke";
        public const string SettingsManage = "settings.manage";

        // Own company (company roles only)
        public const string CompanyProfileView = "company.profile.view";
        public const string CompanyProfileUpdate = "company.profile.update";

        // Users, roles and audit (each domain manages its own)
        public const string UsersView = "users.view";
        public const string UsersManage = "users.manage";
        public const string UsersDeactivate = "users.deactivate";
        public const string UsersResetPassword = "users.reset_password";
        public const string RolesView = "roles.view";
        public const string RolesManage = "roles.manage";
        public const string RolesAssign = "roles.assign";
        public const string AuditView = "audit.view";

        // Properties
        public const string ProjectsCreate = "projects.create";
        public const string ProjectsUpdate = "projects.update";
        public const string ProjectsDelete = "projects.delete";
        public const string BuildingsCreate = "buildings.create";
        public const string BuildingsUpdate = "buildings.update";
        public const string BuildingsDelete = "buildings.delete";
        public const string HousesCreate = "houses.create";
        public const string HousesUpdate = "houses.update";
        public const string HousesDelete = "houses.delete";

        // Company finances and testimonials
        public const string FinancialsView = "financials.view";
        public const string FinancialsManage = "financials.manage";
        public const string TestimonialsManage = "testimonials.manage";

        // Sales and customers
        public const string ReservationsView = "reservations.view";
        public const string ReservationsManage = "reservations.manage";
        public const string CustomersView = "customers.view";
        public const string CustomerDocumentsView = "customers.documents.view";
        public const string NotificationsView = "notifications.view";
        public const string VideoCallsReceive = "communications.video.receive";

        /// <summary>Authorization policy name for "any of these permissions".</summary>
        public static string Policy(params string[] keys) => PermissionPolicy.Prefix + string.Join(PermissionPolicy.Separator, keys);

        /// <summary>Policy: the user is staff with at least one permission (company area shell, dashboards).</summary>
        public const string AnyStaffPolicy = PermissionPolicy.Prefix + PermissionPolicy.AnyStaff;
    }

    public sealed record PermissionDefinition(string Key, string Module, string Name, string Description, PermissionScope Scope);

    public static class PermissionCatalog
    {
        public static readonly IReadOnlyList<PermissionDefinition> All = new List<PermissionDefinition>
        {
            new(Perm.PlatformDashboardView, "Platform administration", "View platform dashboard", "See the platform dashboard with figures across all companies.", PermissionScope.Platform),
            new(Perm.CompaniesView, "Platform administration", "View companies", "See every real estate company, its status and its approval history.", PermissionScope.Platform),
            new(Perm.CompaniesCreate, "Platform administration", "Onboard companies", "Create real estate companies and their first administrator.", PermissionScope.Platform),
            new(Perm.CompaniesUpdate, "Platform administration", "Edit any company", "Change any company's details.", PermissionScope.Platform),
            new(Perm.CompaniesApprove, "Platform administration", "Approve companies", "Approve or reject companies, or ask them for more information.", PermissionScope.Platform),
            new(Perm.CompaniesSuspend, "Platform administration", "Suspend companies", "Suspend an approved company and reactivate it.", PermissionScope.Platform),
            new(Perm.CompaniesDelete, "Platform administration", "Delete companies", "Remove a company from the platform.", PermissionScope.Platform),
            new(Perm.ReportsView, "Platform administration", "View platform reports", "Open and export the platform reports.", PermissionScope.Platform),
            new(Perm.HousesApprove, "Platform administration", "Approve listings", "Approve uploaded apartments and change their approval status.", PermissionScope.Platform),
            new(Perm.OwnerHousesManage, "Platform administration", "Manage owners' houses", "List houses for any individual owner and manage every rent, sale, payment and complaint record.", PermissionScope.Platform),
            new(Perm.KycView, "Customer verification", "View KYC applications", "See customers' identity verification applications and open their documents (each opening is audited).", PermissionScope.Platform),
            new(Perm.KycReview, "Customer verification", "Review KYC applications", "Approve or reject identity verification, or ask the customer to resubmit.", PermissionScope.Platform),
            new(Perm.KycRevoke, "Customer verification", "Revoke verification", "Withdraw an approved verification (the customer loses access to verified-only features).", PermissionScope.Platform),
            new(Perm.SettingsManage, "Platform administration", "Manage platform settings", "Change platform-wide settings such as how long reservations wait for a company.", PermissionScope.Platform),

            new(Perm.CompanyProfileView, "Company profile", "View company profile", "See your company's profile and approval status.", PermissionScope.Company),
            new(Perm.CompanyProfileUpdate, "Company profile", "Edit company profile", "Change your company's details (not its approval status).", PermissionScope.Company),

            new(Perm.UsersView, "Users and roles", "View users", "See staff accounts and the roles they hold.", PermissionScope.Both),
            new(Perm.UsersManage, "Users and roles", "Create and edit users", "Create staff accounts and edit their details.", PermissionScope.Both),
            new(Perm.UsersDeactivate, "Users and roles", "Deactivate users", "Deactivate and reactivate staff accounts.", PermissionScope.Both),
            new(Perm.UsersResetPassword, "Users and roles", "Reset passwords", "Send a staff member a link to set a new password.", PermissionScope.Both),
            new(Perm.RolesView, "Users and roles", "View roles", "See roles and the permissions they contain.", PermissionScope.Both),
            new(Perm.RolesManage, "Users and roles", "Manage roles", "Create, edit, deactivate and delete roles.", PermissionScope.Both),
            new(Perm.RolesAssign, "Users and roles", "Assign roles", "Give roles to staff members and take them away.", PermissionScope.Both),
            new(Perm.AuditView, "Users and roles", "View audit log", "See who changed users, roles, permissions and company status.", PermissionScope.Both),

            new(Perm.ProjectsCreate, "Properties", "Create projects", "Add projects (sites).", PermissionScope.Both),
            new(Perm.ProjectsUpdate, "Properties", "Edit projects", "Change projects and their photos.", PermissionScope.Both),
            new(Perm.ProjectsDelete, "Properties", "Delete projects", "Remove projects.", PermissionScope.Both),
            new(Perm.BuildingsCreate, "Properties", "Create buildings", "Add buildings, one by one or from Excel.", PermissionScope.Both),
            new(Perm.BuildingsUpdate, "Properties", "Edit buildings", "Change buildings.", PermissionScope.Both),
            new(Perm.BuildingsDelete, "Properties", "Delete buildings", "Remove buildings.", PermissionScope.Both),
            new(Perm.HousesCreate, "Properties", "Create apartments", "Add apartments, one by one or by bulk upload (they wait for platform approval).", PermissionScope.Both),
            new(Perm.HousesUpdate, "Properties", "Edit apartments", "Change apartments, their photos, 3D models and walk-throughs.", PermissionScope.Both),
            new(Perm.HousesDelete, "Properties", "Delete apartments", "Remove apartments.", PermissionScope.Both),

            new(Perm.FinancialsView, "Finance", "View financial details", "See company financial details.", PermissionScope.Both),
            new(Perm.FinancialsManage, "Finance", "Manage financial details", "Add and change company financial details.", PermissionScope.Both),
            new(Perm.TestimonialsManage, "Marketing", "Manage testimonials", "Add, change and remove customer testimonials.", PermissionScope.Both),

            new(Perm.ReservationsView, "Sales", "View reservations", "See customers' reservations.", PermissionScope.Both),
            new(Perm.ReservationsManage, "Sales", "Manage reservations", "Approve, cancel or mark reservations as sold.", PermissionScope.Both),
            new(Perm.CustomersView, "Sales", "View customer profiles", "See customer and contact-person profiles.", PermissionScope.Both),
            new(Perm.CustomerDocumentsView, "Sales", "View identity documents", "Open customers' uploaded identity documents.", PermissionScope.Both),
            new(Perm.NotificationsView, "Sales", "View notifications", "See the notification inbox (new reservations, uploads, call requests).", PermissionScope.Both),
            new(Perm.VideoCallsReceive, "Communication", "Answer video calls", "Receive and answer customers' video call requests.", PermissionScope.Both),
        };

        private static readonly Dictionary<string, PermissionDefinition> ByKey = All.ToDictionary(p => p.Key, StringComparer.Ordinal);

        public static PermissionDefinition? Find(string key) => ByKey.TryGetValue(key, out var p) ? p : null;

        /// <summary>Whether a role of this domain may contain the permission.</summary>
        public static bool AllowedIn(PermissionDefinition permission, PermissionScope roleScope) =>
            permission.Scope == PermissionScope.Both || permission.Scope == roleScope;

        public static IEnumerable<PermissionDefinition> For(PermissionScope roleScope) => All.Where(p => AllowedIn(p, roleScope));

        /// <summary>What staff of a company that isn't approved (yet, or any more) may still do.</summary>
        public static readonly IReadOnlySet<string> UnapprovedCompanyPermissions = new HashSet<string>(StringComparer.Ordinal)
        {
            Perm.CompanyProfileView, Perm.CompanyProfileUpdate, Perm.NotificationsView,
        };

        // Built-in roles: their permissions are kept equal to these sets (new catalog entries are added automatically).
        public static readonly Guid PlatformSuperAdminRoleId = new("6f1c2b8e-3d4a-4f55-9a61-0c7e2d9b1a01");
        public const string PlatformSuperAdminRoleName = "Platform Super Admin";
        public const string CompanySuperAdminRoleName = "Company Super Admin";

        public static IEnumerable<string> SuperAdminPermissions(PermissionScope scope) => For(scope).Select(p => p.Key);
    }
}
