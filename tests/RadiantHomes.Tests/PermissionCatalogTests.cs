using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Tests
{
    /// <summary>The permission catalog and the rules that don't need a database.</summary>
    public class PermissionCatalogTests
    {
        private static IEnumerable<string> PermConstants() =>
            typeof(Perm).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name != nameof(Perm.AnyStaffPolicy))
                .Select(f => (string)f.GetRawConstantValue()!);

        [Fact]
        public void Every_permission_constant_is_in_the_catalog_once()
        {
            var keys = PermissionCatalog.All.Select(p => p.Key).ToList();
            Assert.Equal(keys.Count, keys.Distinct().Count());
            foreach (var constant in PermConstants()) Assert.Contains(constant, keys);
            Assert.Equal(PermConstants().Count(), keys.Count);
        }

        [Fact]
        public void Every_permission_has_a_name_description_module_and_a_dotted_key()
        {
            foreach (var p in PermissionCatalog.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(p.Name), p.Key);
                Assert.False(string.IsNullOrWhiteSpace(p.Description), p.Key);
                Assert.False(string.IsNullOrWhiteSpace(p.Module), p.Key);
                Assert.Matches(@"^[a-z_]+(\.[a-z_]+)+$", p.Key);
            }
        }

        [Fact]
        public void Company_roles_can_never_hold_platform_permissions_and_vice_versa()
        {
            Assert.DoesNotContain(PermissionCatalog.For(PermissionScope.Company), p => p.Scope == PermissionScope.Platform);
            Assert.DoesNotContain(PermissionCatalog.For(PermissionScope.Platform), p => p.Scope == PermissionScope.Company);
            Assert.Contains(PermissionCatalog.For(PermissionScope.Company), p => p.Key == Perm.ReservationsManage);
            Assert.DoesNotContain(PermissionCatalog.For(PermissionScope.Company), p => p.Key == Perm.CompaniesApprove);
        }

        [Fact]
        public void Unapproved_companies_keep_only_profile_and_inbox()
        {
            Assert.Equal(new[] { Perm.CompanyProfileUpdate, Perm.CompanyProfileView, Perm.NotificationsView }.OrderBy(k => k),
                PermissionCatalog.UnapprovedCompanyPermissions.OrderBy(k => k));
        }

        [Fact]
        public void Company_staff_can_only_act_inside_their_company()
        {
            var own = Guid.NewGuid();
            var staff = new UserAccess("u", AccountType.CompanyStaff, own, CompanyStatuses.Approved, true, new HashSet<string> { Perm.ProjectsUpdate });
            Assert.True(staff.Can(Perm.ProjectsUpdate, own));
            Assert.False(staff.Can(Perm.ProjectsUpdate, Guid.NewGuid()));
            Assert.False(staff.Can(Perm.ProjectsUpdate, null));
            Assert.False(staff.Can(Perm.ProjectsDelete, own));

            var platform = new UserAccess("p", AccountType.PlatformStaff, null, null, true, new HashSet<string> { Perm.ProjectsUpdate });
            Assert.True(platform.Can(Perm.ProjectsUpdate, Guid.NewGuid()));
            Assert.False(UserAccess.Anonymous.IsStaff);
        }

        [Theory]
        [InlineData(CompanyAction.Approve, CompanyStatuses.Pending, true)]
        [InlineData(CompanyAction.Approve, CompanyStatuses.Suspended, false)]
        [InlineData(CompanyAction.Reject, CompanyStatuses.Approved, false)]
        [InlineData(CompanyAction.RequestInfo, CompanyStatuses.Pending, true)]
        [InlineData(CompanyAction.Suspend, CompanyStatuses.Approved, true)]
        [InlineData(CompanyAction.Suspend, CompanyStatuses.Pending, false)]
        [InlineData(CompanyAction.Reactivate, CompanyStatuses.Suspended, true)]
        [InlineData(CompanyAction.Resubmit, CompanyStatuses.InfoRequested, true)]
        [InlineData(CompanyAction.Resubmit, CompanyStatuses.Approved, false)]
        public void Company_lifecycle_transitions(CompanyAction action, string from, bool allowed)
        {
            Assert.Equal(allowed, CompanyLifecycleService.Rule(action).From.Contains(from));
        }

        [Fact]
        public void Company_actions_are_offered_by_domain_and_permission()
        {
            var company = Guid.NewGuid();
            var approver = new UserAccess("a", AccountType.PlatformStaff, null, null, true, new HashSet<string> { Perm.CompaniesApprove });
            Assert.Equal(new[] { CompanyAction.Approve, CompanyAction.Reject, CompanyAction.RequestInfo },
                CompanyLifecycleService.AvailableActions(approver, company, CompanyStatuses.Pending));

            var viewer = new UserAccess("v", AccountType.PlatformStaff, null, null, true, new HashSet<string> { Perm.CompaniesView });
            Assert.Empty(CompanyLifecycleService.AvailableActions(viewer, company, CompanyStatuses.Pending));

            var companyAdmin = new UserAccess("c", AccountType.CompanyStaff, company, CompanyStatuses.InfoRequested, true, new HashSet<string> { Perm.CompanyProfileUpdate, Perm.CompaniesApprove });
            Assert.Equal(new[] { CompanyAction.Resubmit }, CompanyLifecycleService.AvailableActions(companyAdmin, company, CompanyStatuses.InfoRequested));
        }

        [Fact]
        public async Task Policy_provider_builds_permission_policies_and_rejects_unknown_keys()
        {
            var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
            var policy = await provider.GetPolicyAsync(Perm.Policy(Perm.UsersView, Perm.RolesView));
            var requirement = Assert.Single(policy!.Requirements.OfType<PermissionRequirement>());
            Assert.Equal(new[] { Perm.UsersView, Perm.RolesView }, requirement.Keys);
            Assert.True((await provider.GetPolicyAsync(Perm.AnyStaffPolicy))!.Requirements.OfType<PermissionRequirement>().Single().AnyStaff);
            await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetPolicyAsync("perm:not.a.permission"));
        }
    }
}
