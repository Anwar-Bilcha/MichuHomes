using System.Net;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;
using RadiantHomes.Tests.Infrastructure;

namespace RadiantHomes.Tests
{
    /// <summary>Direct URL access through the running app: every protected page checks permissions on the server.</summary>
    [Collection(DatabaseCollection.Name)]
    public class AuthorizationHttpTests : IAsyncLifetime
    {
        private readonly TestDatabase _database;
        private AppFactory _app = null!;

        public AuthorizationHttpTests(TestDatabase database) => _database = database;

        public Task InitializeAsync()
        {
            _app = new AppFactory(_database);
            return Task.CompletedTask;
        }

        public async Task DisposeAsync() => await _app.DisposeAsync();

        [Theory]
        [InlineData("/Admin/Dashboard/AdminDashboard")]
        [InlineData("/Admin/Companies")]
        [InlineData("/Staff/Users")]
        [InlineData("/Staff/Roles")]
        [InlineData("/Staff/Audit")]
        [InlineData("/HousingCompanyAdmin")]
        [InlineData("/Projects/Create")]
        public async Task Anonymous_visitors_are_challenged(string url)
        {
            var response = await _app.ClientFor(null).GetAsync(url);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/ApartmentHouses")]
        [InlineData("/RealEstates")]
        [InlineData("/Identity/Account/Login")]
        [InlineData("/Identity/Account/ForgotPassword")]
        public async Task Public_pages_stay_public(string url)
        {
            var response = await _app.ClientFor(null).GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Pages_follow_the_users_permissions_not_a_role_name()
        {
            await using var db = _database.NewContext();
            var role = await Seed.RoleAsync(db, PermissionScope.Platform, null, Perm.CompaniesView);
            var viewer = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, role.Id);
            var client = _app.ClientFor(viewer.Id);

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin/Companies")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Admin/Dashboard/AdminDashboard")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Staff/Users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Admin/Reports/AdminReports")).StatusCode);

            // Revoking the permission applies on the next request.
            db.AccessRolePermissions.RemoveRange(db.AccessRolePermissions.Where(p => p.RoleId == role.Id));
            await db.SaveChangesAsync();
            AccessControlService.Invalidate();
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Admin/Companies")).StatusCode);
        }

        [Fact]
        public async Task Company_staff_reach_their_area_but_not_the_platform_or_other_companies()
        {
            await using var db = _database.NewContext();
            var company = await Seed.CompanyAsync(db);
            var other = await Seed.CompanyAsync(db);
            var admin = await Seed.StaffAsync(db, AccountType.CompanyStaff, company.CompanyId, (await Seed.CompanySuperAdminAsync(db, company.CompanyId)).Id);
            var otherRole = await Seed.CompanySuperAdminAsync(db, other.CompanyId);
            var otherUser = await Seed.StaffAsync(db, AccountType.CompanyStaff, other.CompanyId, otherRole.Id);
            var client = _app.ClientFor(admin.Id);

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/HousingCompanyAdmin")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Staff/Users")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Projects/Create")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Admin/Companies")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/RealEstates/Create")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Staff/Roles/Edit?id={otherRole.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Staff/Users/Edit?id={otherUser.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Staff/Roles/Edit?id={PermissionCatalog.PlatformSuperAdminRoleId}")).StatusCode);
        }

        [Fact]
        public async Task Staff_of_a_pending_company_only_see_their_status_and_profile()
        {
            await using var db = _database.NewContext();
            var company = await Seed.CompanyAsync(db, CompanyStatuses.Pending);
            var admin = await Seed.StaffAsync(db, AccountType.CompanyStaff, company.CompanyId, (await Seed.CompanySuperAdminAsync(db, company.CompanyId)).Id);
            var client = _app.ClientFor(admin.Id);

            var status = await client.GetAsync("/HousingCompanyAdmin/Status");
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
            Assert.Contains("Michu Homes is reviewing your company", await status.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/RealEstates/Edit?id={company.CompanyId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Projects/Create")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Staff/Users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/HousingCompanyAdmin/Reservations")).StatusCode);
        }

        [Fact]
        public async Task Customers_get_no_staff_pages()
        {
            await using var db = _database.NewContext();
            var customer = await Seed.StaffAsync(db, AccountType.Customer, null);
            var client = _app.ClientFor(customer.Id);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Staff/Users")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/HousingCompanyAdmin")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Admin/Notifications/NotificationsAll")).StatusCode);
        }
    }
}
