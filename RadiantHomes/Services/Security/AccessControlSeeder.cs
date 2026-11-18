using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models.Security;

namespace RadiantHomes.Services.Security
{
    /// <summary>
    /// Start-up sync: the Permissions table follows the catalog in code, and the built-in roles exist with exactly
    /// their catalog permissions (Platform Super Admin once; Company Super Admin for every company). Safe to run on
    /// every start; changes nothing when everything is already in place.
    /// </summary>
    public static class AccessControlSeeder
    {
        public static async Task SyncAsync(ApplicationDbContext db, ILogger logger)
        {
            // 1. Permission catalog
            var existing = await db.Permissions.ToDictionaryAsync(p => p.Key, StringComparer.Ordinal);
            var order = 0;
            foreach (var def in PermissionCatalog.All)
            {
                order++;
                if (!existing.TryGetValue(def.Key, out var row))
                {
                    db.Permissions.Add(new Permission { Key = def.Key, Module = def.Module, Name = def.Name, Description = def.Description, Scope = def.Scope, SortOrder = order });
                }
                else if (row.Module != def.Module || row.Name != def.Name || row.Description != def.Description || row.Scope != def.Scope || row.SortOrder != order)
                {
                    row.Module = def.Module; row.Name = def.Name; row.Description = def.Description; row.Scope = def.Scope; row.SortOrder = order;
                }
            }
            await db.SaveChangesAsync();

            // 2. Platform Super Admin
            var platform = await db.AccessRoles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == PermissionCatalog.PlatformSuperAdminRoleId);
            if (platform is null)
            {
                platform = new AccessRole
                {
                    Id = PermissionCatalog.PlatformSuperAdminRoleId,
                    Name = PermissionCatalog.PlatformSuperAdminRoleName,
                    Description = "Built-in. Every platform permission.",
                    Scope = PermissionScope.Platform,
                    IsSystem = true,
                };
                db.AccessRoles.Add(platform);
            }
            SetPermissions(db, platform, PermissionCatalog.SuperAdminPermissions(PermissionScope.Platform));

            // 3. A Company Super Admin role for every company
            var companyIds = await db.HousingCompanies.Select(c => c.CompanyId).ToListAsync();
            var companyAdmins = await db.AccessRoles.Include(r => r.Permissions)
                .Where(r => r.IsSystem && r.Scope == PermissionScope.Company && r.Name == PermissionCatalog.CompanySuperAdminRoleName)
                .ToListAsync();
            var companyKeys = PermissionCatalog.SuperAdminPermissions(PermissionScope.Company).ToList();
            foreach (var companyId in companyIds)
            {
                var role = companyAdmins.FirstOrDefault(r => r.CompanyId == companyId) ?? NewCompanySuperAdmin(db, companyId);
                SetPermissions(db, role, companyKeys);
            }

            var changes = await db.SaveChangesAsync();
            if (changes > 0)
            {
                logger.LogInformation("Access control: permission catalog and built-in roles updated ({Changes} changes).", changes);
                AccessControlService.Invalidate();
            }
        }

        /// <summary>Creates (stages) the built-in Company Super Admin role of a company.</summary>
        public static AccessRole NewCompanySuperAdmin(ApplicationDbContext db, Guid companyId)
        {
            var role = new AccessRole
            {
                Id = Guid.NewGuid(),
                Name = PermissionCatalog.CompanySuperAdminRoleName,
                Description = "Built-in. Every company permission, including managing the company's users and roles.",
                Scope = PermissionScope.Company,
                CompanyId = companyId,
                IsSystem = true,
            };
            db.AccessRoles.Add(role);
            SetPermissions(db, role, PermissionCatalog.SuperAdminPermissions(PermissionScope.Company));
            return role;
        }

        private static void SetPermissions(ApplicationDbContext db, AccessRole role, IEnumerable<string> keys)
        {
            var wanted = keys.ToHashSet(StringComparer.Ordinal);
            foreach (var extra in role.Permissions.Where(p => !wanted.Contains(p.PermissionKey)).ToList())
            {
                role.Permissions.Remove(extra);
                db.AccessRolePermissions.Remove(extra);
            }
            foreach (var key in wanted.Where(k => role.Permissions.All(p => p.PermissionKey != k)))
            {
                role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, PermissionKey = key });
            }
        }
    }
}
