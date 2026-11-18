using Microsoft.EntityFrameworkCore;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;
using RadiantHomes.Tests.Infrastructure;

namespace RadiantHomes.Tests
{
    /// <summary>Effective permissions, management rules and the company lifecycle against a real (throw-away) database.</summary>
    [Collection(DatabaseCollection.Name)]
    public class AccessControlTests
    {
        private readonly TestDatabase _database;
        public AccessControlTests(TestDatabase database) => _database = database;

        // ---------- Effective permissions ----------

        [Fact]
        public async Task Migrations_and_sync_create_the_catalog_and_built_in_roles()
        {
            await using var db = _database.NewContext();
            Assert.Equal(PermissionCatalog.All.Count, await db.Permissions.CountAsync());
            var platform = await db.AccessRoles.Include(r => r.Permissions).SingleAsync(r => r.Id == PermissionCatalog.PlatformSuperAdminRoleId);
            Assert.True(platform.IsSystem);
            Assert.Equal(PermissionCatalog.For(PermissionScope.Platform).Count(), platform.Permissions.Count);
        }

        [Fact]
        public async Task Permissions_come_only_from_active_roles_of_the_users_own_domain()
        {
            await using var db = _database.NewContext();
            var company = await Seed.CompanyAsync(db);
            var other = await Seed.CompanyAsync(db);
            var mine = await Seed.RoleAsync(db, PermissionScope.Company, company.CompanyId, Perm.ProjectsCreate);
            var inactive = await Seed.RoleAsync(db, PermissionScope.Company, company.CompanyId, Perm.ProjectsDelete);
            inactive.IsActive = false;
            var foreign = await Seed.RoleAsync(db, PermissionScope.Company, other.CompanyId, Perm.BuildingsCreate);
            await db.SaveChangesAsync();
            var user = await Seed.StaffAsync(db, AccountType.CompanyStaff, company.CompanyId, mine.Id, inactive.Id, foreign.Id);

            var access = await Seed.AccessOfAsync(_database, user.Id);
            Assert.Equal(new[] { Perm.ProjectsCreate }, access.Permissions);
            Assert.Equal(company.CompanyId, access.CompanyId);
        }

        [Fact]
        public async Task Platform_permissions_in_a_company_role_never_count()
        {
            await using var db = _database.NewContext();
            var company = await Seed.CompanyAsync(db);
            var role = await Seed.RoleAsync(db, PermissionScope.Company, company.CompanyId, Perm.ReservationsView, Perm.CompaniesApprove); // written past the service
            var user = await Seed.StaffAsync(db, AccountType.CompanyStaff, company.CompanyId, role.Id);
            var access = await Seed.AccessOfAsync(_database, user.Id);
            Assert.True(access.Has(Perm.ReservationsView));
            Assert.False(access.Has(Perm.CompaniesApprove));
        }

        [Theory]
        [InlineData(CompanyStatuses.Pending)]
        [InlineData(CompanyStatuses.Rejected)]
        [InlineData(CompanyStatuses.Suspended)]
        public async Task Staff_of_unapproved_companies_keep_only_profile_and_inbox(string status)
        {
            await using var db = _database.NewContext();
            var company = await Seed.CompanyAsync(db, status);
            var super = await Seed.CompanySuperAdminAsync(db, company.CompanyId);
            var user = await Seed.StaffAsync(db, AccountType.CompanyStaff, company.CompanyId, super.Id);
            var access = await Seed.AccessOfAsync(_database, user.Id);
            Assert.Equal(PermissionCatalog.UnapprovedCompanyPermissions.OrderBy(k => k), access.Permissions.OrderBy(k => k));
            Assert.False(access.Has(Perm.ProjectsCreate));
        }

        [Fact]
        public async Task Deactivated_accounts_and_customers_have_no_permissions()
        {
            await using var db = _database.NewContext();
            var platformAdmin = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, PermissionCatalog.PlatformSuperAdminRoleId);
            Assert.True((await Seed.AccessOfAsync(_database, platformAdmin.Id)).Has(Perm.CompaniesApprove));

            platformAdmin.LockoutEnd = DateTimeOffset.MaxValue;
            await db.SaveChangesAsync();
            var access = await Seed.AccessOfAsync(_database, platformAdmin.Id);
            Assert.False(access.IsActive);
            Assert.Empty(access.Permissions);

            var customer = await Seed.StaffAsync(db, AccountType.Customer, null, PermissionCatalog.PlatformSuperAdminRoleId); // role ignored
            Assert.Empty((await Seed.AccessOfAsync(_database, customer.Id)).Permissions);
        }

        // ---------- Management rules ----------

        [Fact]
        public async Task Company_admins_cannot_grant_permissions_they_do_not_hold()
        {
            await using var db = _database.NewContext();
            var company = await Seed.CompanyAsync(db);
            var limited = await Seed.RoleAsync(db, PermissionScope.Company, company.CompanyId, Perm.RolesManage, Perm.ReservationsView);
            var admin = await Seed.StaffAsync(db, AccountType.CompanyStaff, company.CompanyId, limited.Id);

            await using var s = new AppServices(_database, admin.Id);
            var actor = await s.Get<AccessControlService>().LoadAsync(admin.Id);
            var refused = await s.Get<AccessManagementService>().CreateRoleAsync(actor, new RoleInput { Name = "Too much", Permissions = { Perm.ReservationsView, Perm.ProjectsDelete } });
            Assert.False(refused.Succeeded);
            Assert.Contains("only add permissions you have", refused.Message);

            var allowed = await s.Get<AccessManagementService>().CreateRoleAsync(actor, new RoleInput { Name = "Viewer", Permissions = { Perm.ReservationsView } });
            Assert.True(allowed.Succeeded, allowed.Message);
            var created = await db.AccessRoles.Include(r => r.Permissions).SingleAsync(r => r.Id == Guid.Parse(allowed.Id!));
            Assert.Equal(company.CompanyId, created.CompanyId); // always the actor's own company
            Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityId == allowed.Id && a.Action == "RoleCreated"));
        }

        [Fact]
        public async Task Roles_of_another_company_or_domain_cannot_be_assigned()
        {
            await using var db = _database.NewContext();
            var company = await Seed.CompanyAsync(db);
            var other = await Seed.CompanyAsync(db);
            var admin = await Seed.StaffAsync(db, AccountType.CompanyStaff, company.CompanyId, (await Seed.CompanySuperAdminAsync(db, company.CompanyId)).Id);
            var colleague = await Seed.StaffAsync(db, AccountType.CompanyStaff, company.CompanyId);
            var foreignRole = await Seed.RoleAsync(db, PermissionScope.Company, other.CompanyId, Perm.ReservationsView);

            await using var s = new AppServices(_database, admin.Id);
            var actor = await s.Get<AccessControlService>().LoadAsync(admin.Id);
            var manage = s.Get<AccessManagementService>();
            Assert.False((await manage.SetUserRolesAsync(actor, colleague.Id, new[] { foreignRole.Id })).Succeeded);
            Assert.False((await manage.SetUserRolesAsync(actor, colleague.Id, new[] { PermissionCatalog.PlatformSuperAdminRoleId })).Succeeded);

            var otherStaff = await Seed.StaffAsync(db, AccountType.CompanyStaff, other.CompanyId);
            Assert.False((await manage.SetUserActiveAsync(actor, otherStaff.Id, false)).Succeeded); // not visible to this company
        }

        [Fact]
        public async Task Nobody_changes_their_own_roles_and_the_last_super_admin_is_protected()
        {
            await using var db = _database.NewContext();
            var company = await Seed.CompanyAsync(db);
            var super = await Seed.CompanySuperAdminAsync(db, company.CompanyId);
            var only = await Seed.StaffAsync(db, AccountType.CompanyStaff, company.CompanyId, super.Id);
            var platformAdmin = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, PermissionCatalog.PlatformSuperAdminRoleId);

            await using var self = new AppServices(_database, only.Id);
            var selfAccess = await self.Get<AccessControlService>().LoadAsync(only.Id);
            Assert.False((await self.Get<AccessManagementService>().SetUserRolesAsync(selfAccess, only.Id, Array.Empty<Guid>())).Succeeded);
            Assert.False((await self.Get<AccessManagementService>().SetUserActiveAsync(selfAccess, only.Id, false)).Succeeded);

            await using var platform = new AppServices(_database, platformAdmin.Id);
            var platformAccess = await platform.Get<AccessControlService>().LoadAsync(platformAdmin.Id);
            var removeLast = await platform.Get<AccessManagementService>().SetUserRolesAsync(platformAccess, only.Id, Array.Empty<Guid>());
            Assert.False(removeLast.Succeeded);
            Assert.Contains("last active holder", removeLast.Message);
            Assert.False((await platform.Get<AccessManagementService>().SetUserActiveAsync(platformAccess, only.Id, false)).Succeeded);

            await Seed.StaffAsync(db, AccountType.CompanyStaff, company.CompanyId, super.Id); // a second holder
            Assert.True((await platform.Get<AccessManagementService>().SetUserActiveAsync(platformAccess, only.Id, false)).Succeeded);
        }

        [Fact]
        public async Task Roles_still_held_by_people_cannot_be_deleted_and_built_in_roles_never()
        {
            await using var db = _database.NewContext();
            var platformAdmin = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, PermissionCatalog.PlatformSuperAdminRoleId);
            var role = await Seed.RoleAsync(db, PermissionScope.Platform, null, Perm.ReportsView);
            await Seed.StaffAsync(db, AccountType.PlatformStaff, null, role.Id);

            await using var s = new AppServices(_database, platformAdmin.Id);
            var actor = await s.Get<AccessControlService>().LoadAsync(platformAdmin.Id);
            var manage = s.Get<AccessManagementService>();
            Assert.False((await manage.DeleteRoleAsync(actor, role.Id)).Succeeded);
            Assert.False((await manage.DeleteRoleAsync(actor, PermissionCatalog.PlatformSuperAdminRoleId)).Succeeded);
            Assert.False((await manage.UpdateRoleAsync(actor, PermissionCatalog.PlatformSuperAdminRoleId, new RoleInput { Name = "x" })).Succeeded);
        }

        // ---------- Company lifecycle ----------

        [Fact]
        public async Task New_companies_start_pending_with_history_and_their_super_admin_role()
        {
            await using var db = _database.NewContext();
            var company = await Seed.CompanyAsync(db, CompanyStatuses.Pending);
            Assert.Equal(CompanyStatuses.Pending, (await db.HousingCompanies.AsNoTracking().SingleAsync(c => c.CompanyId == company.CompanyId)).Status);
            Assert.True(await db.CompanyStatusChanges.AnyAsync(h => h.CompanyId == company.CompanyId && h.ToStatus == CompanyStatuses.Pending && h.FromStatus == null));
            Assert.NotNull(await Seed.CompanySuperAdminAsync(db, company.CompanyId));
        }

        [Fact]
        public async Task Ordinary_saves_cannot_change_a_company_status()
        {
            await using var db = _database.NewContext();
            var company = await Seed.CompanyAsync(db, CompanyStatuses.Pending);

            await using var form = _database.NewContext();
            var posted = await form.HousingCompanies.AsNoTracking().SingleAsync(c => c.CompanyId == company.CompanyId);
            posted.Status = CompanyStatuses.Approved;
            posted.Name += " (edited)";
            form.Attach(posted).State = EntityState.Modified; // what edit pages do
            await form.SaveChangesAsync();

            var stored = await _database.NewContext().HousingCompanies.AsNoTracking().SingleAsync(c => c.CompanyId == company.CompanyId);
            Assert.Equal(CompanyStatuses.Pending, stored.Status);
            Assert.EndsWith("(edited)", stored.Name);
        }

        [Fact]
        public async Task Approval_decisions_need_the_platform_permission_a_reason_where_required_and_are_recorded()
        {
            await using var db = _database.NewContext();
            var company = await Seed.CompanyAsync(db, CompanyStatuses.Pending);
            var approverRole = await Seed.RoleAsync(db, PermissionScope.Platform, null, Perm.CompaniesView, Perm.CompaniesApprove);
            var approver = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, approverRole.Id);
            var viewerRole = await Seed.RoleAsync(db, PermissionScope.Platform, null, Perm.CompaniesView);
            var viewer = await Seed.StaffAsync(db, AccountType.PlatformStaff, null, viewerRole.Id);

            await using (var v = new AppServices(_database, viewer.Id))
            {
                var access = await v.Get<AccessControlService>().LoadAsync(viewer.Id);
                Assert.False((await v.Get<CompanyLifecycleService>().ChangeAsync(access, company.CompanyId, CompanyAction.Approve, null)).Succeeded);
            }

            await using var s = new AppServices(_database, approver.Id);
            var actor = await s.Get<AccessControlService>().LoadAsync(approver.Id);
            var lifecycle = s.Get<CompanyLifecycleService>();
            Assert.False((await lifecycle.ChangeAsync(actor, company.CompanyId, CompanyAction.Reject, "  ")).Succeeded); // reason required
            Assert.False((await lifecycle.ChangeAsync(actor, company.CompanyId, CompanyAction.Suspend, "x")).Succeeded); // not approved yet
            Assert.True((await lifecycle.ChangeAsync(actor, company.CompanyId, CompanyAction.RequestInfo, "Send your licence")).Succeeded);
            Assert.True((await lifecycle.ChangeAsync(actor, company.CompanyId, CompanyAction.Approve, null)).Succeeded);

            await using var check = _database.NewContext();
            Assert.Equal(CompanyStatuses.Approved, (await check.HousingCompanies.AsNoTracking().SingleAsync(c => c.CompanyId == company.CompanyId)).Status);
            var history = await check.CompanyStatusChanges.Where(h => h.CompanyId == company.CompanyId).OrderBy(h => h.Id).Select(h => h.ToStatus).ToListAsync();
            Assert.Equal(new[] { CompanyStatuses.Pending, CompanyStatuses.InfoRequested, CompanyStatuses.Approved }, history);
            Assert.True(await check.Notifications.AnyAsync(n => n.ReceiverCompanyId == company.CompanyId && n.Title == "Company approved"));
            Assert.Equal(2, await check.AuditLogs.CountAsync(a => a.EntityId == company.CompanyId.ToString() && a.Category == AuditCategories.Companies));
        }
    }
}
