using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Tests.Infrastructure
{
    /// <summary>Test data: companies, staff accounts and roles, created straight in the database.</summary>
    public static class Seed
    {
        public static async Task<HousingCompany> CompanyAsync(ApplicationDbContext db, string status = CompanyStatuses.Approved)
        {
            var address = new PartyAddress { AddressID = Guid.NewGuid(), City = "Addis Ababa", Country = "Ethiopia", Street = "Test street" };
            var company = new HousingCompany
            {
                CompanyId = Guid.NewGuid(),
                Name = "Test company " + Guid.NewGuid().ToString("N")[..6],
                LicenseNumber = "LIC",
                EstablishedDate = new DateTime(2020, 1, 1),
                AddressId = address.AddressID,
                Address = address,
                Email = "info@test.example",
                PhoneNumber = "+251900000000",
                Website = "https://test.example",
                LogoUrl = string.Empty,
            };
            db.HousingCompanies.Add(company);
            await db.SaveChangesAsync(); // starts Pending (DbContext guard) with its Company Super Admin role
            if (status != CompanyStatuses.Pending)
            {
                db.CompanyStatusChangeAllowed = true;
                company.Status = status;
                await db.SaveChangesAsync();
                db.CompanyStatusChangeAllowed = false;
            }
            return company;
        }

        public static async Task<AppUser> StaffAsync(ApplicationDbContext db, AccountType type, Guid? companyId, params Guid[] roleIds)
        {
            var email = $"staff.{Guid.NewGuid():N}@test.example";
            var user = new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                FullName = "Test " + type,
                AccountType = type,
                HousingCompanyId = companyId,
                LockoutEnabled = true,
            };
            db.AppUsers.Add(user);
            foreach (var roleId in roleIds) db.UserAccessRoles.Add(new UserAccessRole { UserId = user.Id, RoleId = roleId, AssignedOn = DateTime.UtcNow });
            await db.SaveChangesAsync();
            AccessControlService.Invalidate();
            return user;
        }

        public static async Task<AccessRole> RoleAsync(ApplicationDbContext db, PermissionScope scope, Guid? companyId, params string[] permissions)
        {
            var role = new AccessRole { Id = Guid.NewGuid(), Name = "Role " + Guid.NewGuid().ToString("N")[..6], Scope = scope, CompanyId = companyId };
            foreach (var key in permissions) role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, PermissionKey = key });
            db.AccessRoles.Add(role);
            await db.SaveChangesAsync();
            return role;
        }

        public static Task<AccessRole> CompanySuperAdminAsync(ApplicationDbContext db, Guid companyId) =>
            db.AccessRoles.SingleAsync(r => r.CompanyId == companyId && r.IsSystem && r.Name == PermissionCatalog.CompanySuperAdminRoleName);

        public static async Task<UserAccess> AccessOfAsync(TestDatabase database, string userId)
        {
            await using var db = database.NewContext();
            return await new AccessControlService(db, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())).LoadAsync(userId);
        }
    }
}
