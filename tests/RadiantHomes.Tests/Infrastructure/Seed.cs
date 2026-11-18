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

        public sealed record Listing(HousingCompany Company, Building Building, ApartmentHouse House, Customer ContactPerson);

        /// <summary>An approved company with a contact person, a project, a 10-floor building and one listed apartment.</summary>
        public static async Task<Listing> ListingAsync(ApplicationDbContext db)
        {
            var company = await CompanyAsync(db);
            var contact = await CustomerAsync(db, company.CompanyId);
            var project = new HouseProject { ProjectId = Guid.NewGuid(), ProjectName = "Test site", AddressId = company.AddressId, StartDate = DateTime.Today, TotalArea = 1000, HousingCompanyId = company.CompanyId };
            var building = new Building { Id = Guid.NewGuid(), BuildingName = "Block A", BuildingType = "Residential", ConstructionStatus = "Completed", NumberOfFloors = 10, ProjectId = project.ProjectId, AddressId = company.AddressId };
            var category = await db.HouseCategories.FirstOrDefaultAsync() ?? new HouseCategory { Id = Guid.NewGuid(), Name = "Serviced apartment" };
            if (db.Entry(category).State == EntityState.Detached) db.HouseCategories.Add(category);
            db.Projects.Add(project);
            db.Buildings.Add(building);
            await db.SaveChangesAsync();
            var house = await ApartmentAsync(db, building, company.AddressId, category.Id, "A-" + Guid.NewGuid().ToString("N")[..4]);
            return new Listing(company, building, house, contact);
        }

        public static async Task<ApartmentHouse> ApartmentAsync(ApplicationDbContext db, Building building, Guid addressId, Guid categoryId, string number, string status = "Approved")
        {
            var house = new ApartmentHouse
            {
                Id = Guid.NewGuid(), Title = "Test apartment " + number, HouseNumber = number, BuildingId = building.Id, AddressId = addressId, HouseCategoryId = categoryId,
                Currency = "ETB", SalePrice = 5_000_000, FloorNumber = 2, Bedrooms = 2, TotalRooms = 4, Bathrooms = 1, LivingRooms = 1, Area = 90,
                HouseType = "Residential", SalesStatus = "None", Status = status, IsAvailable = true, Description = "Test",
            };
            db.ApartmentHouses.Add(house);
            await db.SaveChangesAsync();
            return house;
        }

        /// <summary>A customer profile (with a sign-in account when <paramref name="withAccount"/>), optionally a company's contact person.</summary>
        public static async Task<Customer> CustomerAsync(ApplicationDbContext db, Guid? companyId = null, string kycStatus = Models.Kyc.KycStatuses.NotSubmitted)
        {
            var ranking = await db.HomeOwnerRankings.FirstOrDefaultAsync() ?? new HomeOwnerRanking { RankingId = Guid.NewGuid(), RankName = "Standard" };
            if (db.Entry(ranking).State == EntityState.Detached) db.HomeOwnerRankings.Add(ranking);
            var address = new PartyAddress { AddressID = Guid.NewGuid(), City = "Addis Ababa", Country = "Ethiopia", Street = "Customer street" };
            var customer = new Customer
            {
                Id = Guid.NewGuid(), FirstName = "Test", LastName = "Customer " + Guid.NewGuid().ToString("N")[..4], RankingId = ranking.RankingId,
                AddressId = address.AddressID, Address = address, CompanyId = companyId, Email = $"c.{Guid.NewGuid():N}@test.example", KycStatus = kycStatus,
            };
            db.UserProfiles.Add(customer);
            await db.SaveChangesAsync();
            return customer;
        }

        public static async Task<AppUser> CustomerAccountAsync(ApplicationDbContext db, Customer customer)
        {
            var user = await StaffAsync(db, AccountType.Customer, null);
            user.CustomerId = customer.Id;
            await db.SaveChangesAsync();
            return user;
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
