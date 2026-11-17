using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Security;

namespace RadiantHomes.Services.Security
{
    public sealed record AccessResult(bool Succeeded, string Message, string? Id = null)
    {
        public static AccessResult Ok(string message, string? id = null) => new(true, message, id);
        public static AccessResult Fail(string message) => new(false, message);
    }

    public sealed class RoleInput
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public List<string> Permissions { get; set; } = new();
        /// <summary>Platform staff creating a company's role (oversight). Ignored for company staff (always their own company).</summary>
        public Guid? CompanyId { get; set; }
    }

    public sealed class StaffUserInput
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        /// <summary>Optional first password. Without one, the person sets it from a password-reset (invitation) link.</summary>
        public string? Password { get; set; }
        /// <summary>Platform staff creating a company's user. Null: a platform user. Ignored for company staff.</summary>
        public Guid? CompanyId { get; set; }
        public List<Guid> RoleIds { get; set; } = new();
    }

    /// <summary>
    /// Business rules for managing staff users and roles in both administrative domains:
    /// platform staff manage platform users/roles and (oversight) any company's; company staff manage their own company's.
    /// Roles and users of different domains or companies never mix; you can only grant permissions you hold yourself
    /// (platform oversight of a company is the exception); nobody changes their own roles or deactivates themselves; the
    /// last active super admin of a domain is protected. Every change is audited.
    /// </summary>
    public sealed class AccessManagementService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<AppUser> _users;
        private readonly AuditService _audit;

        public AccessManagementService(ApplicationDbContext db, UserManager<AppUser> users, AuditService audit)
        {
            _db = db;
            _users = users;
            _audit = audit;
        }

        // ---------- What the actor can see ----------

        public IQueryable<AccessRole> VisibleRoles(UserAccess actor) => actor.IsPlatform
            ? _db.AccessRoles
            : _db.AccessRoles.Where(r => r.Scope == PermissionScope.Company && r.CompanyId == actor.CompanyId && actor.CompanyId != null);

        public IQueryable<AppUser> VisibleStaff(UserAccess actor) => actor.IsPlatform
            ? _db.AppUsers.Where(u => u.AccountType != AccountType.Customer)
            : _db.AppUsers.Where(u => u.AccountType == AccountType.CompanyStaff && u.HousingCompanyId == actor.CompanyId && actor.CompanyId != null);

        /// <summary>Roles that may be given to this staff member (same domain; company roles of the same company).</summary>
        public IQueryable<AccessRole> AssignableRoles(AppUser target) => target.AccountType == AccountType.PlatformStaff
            ? _db.AccessRoles.Where(r => r.Scope == PermissionScope.Platform && r.CompanyId == null)
            : _db.AccessRoles.Where(r => r.Scope == PermissionScope.Company && r.CompanyId == target.HousingCompanyId && target.HousingCompanyId != null);

        public static bool IsSuperAdminRole(AccessRole role) =>
            role.IsSystem && (role.Id == PermissionCatalog.PlatformSuperAdminRoleId || role.Name == PermissionCatalog.CompanySuperAdminRoleName);

        // ---------- Roles ----------

        public async Task<AccessResult> CreateRoleAsync(UserAccess actor, RoleInput input)
        {
            if (!actor.Has(Perm.RolesManage)) return AccessResult.Fail("You don't have permission to manage roles.");
            var (scope, companyId, error) = await TargetDomainAsync(actor, input.CompanyId);
            if (error is not null) return AccessResult.Fail(error);

            var name = (input.Name ?? string.Empty).Trim();
            // Every permission of a new role is newly granted: the delegation rule applies to all of them.
            var check = await ValidateRoleAsync(actor, scope, companyId, name, null, input.Permissions, input.Permissions);
            if (check is not null) return AccessResult.Fail(check);

            var role = new AccessRole
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                Scope = scope,
                CompanyId = companyId,
                IsActive = input.IsActive,
            };
            foreach (var key in input.Permissions.Distinct()) role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, PermissionKey = key });
            _db.AccessRoles.Add(role);
            _audit.Add(AuditCategories.Roles, "RoleCreated", $"Role \"{role.Name}\" created with {role.Permissions.Count} permissions.",
                companyId, nameof(AccessRole), role.Id.ToString(), new { role.Name, role.Scope, role.IsActive, Permissions = input.Permissions.Distinct().OrderBy(k => k) });
            await _db.SaveChangesAsync();
            AccessControlService.Invalidate();
            return AccessResult.Ok($"Role \"{role.Name}\" was created.", role.Id.ToString());
        }

        public async Task<AccessResult> UpdateRoleAsync(UserAccess actor, Guid roleId, RoleInput input)
        {
            if (!actor.Has(Perm.RolesManage)) return AccessResult.Fail("You don't have permission to manage roles.");
            var role = await VisibleRoles(actor).Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId);
            if (role is null) return AccessResult.Fail("That role doesn't exist or isn't yours to manage.");
            if (role.IsSystem) return AccessResult.Fail("Built-in roles can't be changed.");

            var name = (input.Name ?? string.Empty).Trim();
            var current = role.Permissions.Select(p => p.PermissionKey).ToHashSet(StringComparer.Ordinal);
            var wanted = input.Permissions.Distinct().ToHashSet(StringComparer.Ordinal);
            var added = wanted.Except(current).ToList();
            var removed = current.Except(wanted).ToList();
            var check = await ValidateRoleAsync(actor, role.Scope, role.CompanyId, name, role.Id, wanted, added);
            if (check is not null) return AccessResult.Fail(check);

            var before = new { role.Name, role.Description, role.IsActive };
            role.Name = name;
            role.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            role.IsActive = input.IsActive;
            foreach (var p in role.Permissions.Where(p => removed.Contains(p.PermissionKey)).ToList())
            {
                role.Permissions.Remove(p);
                _db.AccessRolePermissions.Remove(p);
            }
            foreach (var key in added) role.Permissions.Add(new AccessRolePermission { RoleId = role.Id, PermissionKey = key });

            _audit.Add(AuditCategories.Roles, "RoleUpdated",
                $"Role \"{role.Name}\" updated: {added.Count} permissions added, {removed.Count} removed{(before.IsActive != role.IsActive ? (role.IsActive ? ", reactivated" : ", deactivated") : "")}.",
                role.CompanyId, nameof(AccessRole), role.Id.ToString(),
                new { Before = before, After = new { role.Name, role.Description, role.IsActive }, Added = added.OrderBy(k => k), Removed = removed.OrderBy(k => k) });
            await _db.SaveChangesAsync();
            AccessControlService.Invalidate();
            return AccessResult.Ok($"Role \"{role.Name}\" was saved.");
        }

        public async Task<AccessResult> DeleteRoleAsync(UserAccess actor, Guid roleId)
        {
            if (!actor.Has(Perm.RolesManage)) return AccessResult.Fail("You don't have permission to manage roles.");
            var role = await VisibleRoles(actor).Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId);
            if (role is null) return AccessResult.Fail("That role doesn't exist or isn't yours to manage.");
            if (role.IsSystem) return AccessResult.Fail("Built-in roles can't be deleted.");
            var holders = await _db.UserAccessRoles.CountAsync(ur => ur.RoleId == roleId);
            if (holders > 0) return AccessResult.Fail($"\"{role.Name}\" is still given to {holders} {(holders == 1 ? "person" : "people")}. Give them another role first, or deactivate the role instead.");

            _db.AccessRolePermissions.RemoveRange(role.Permissions);
            _db.AccessRoles.Remove(role);
            _audit.Add(AuditCategories.Roles, "RoleDeleted", $"Role \"{role.Name}\" deleted.", role.CompanyId, nameof(AccessRole), role.Id.ToString(),
                new { role.Name, Permissions = role.Permissions.Select(p => p.PermissionKey).OrderBy(k => k) });
            await _db.SaveChangesAsync();
            AccessControlService.Invalidate();
            return AccessResult.Ok($"Role \"{role.Name}\" was deleted.");
        }

        // ---------- Users ----------

        public async Task<AccessResult> CreateStaffUserAsync(UserAccess actor, StaffUserInput input)
        {
            if (!actor.Has(Perm.UsersManage)) return AccessResult.Fail("You don't have permission to create users.");
            var (scope, companyId, error) = await TargetDomainAsync(actor, input.CompanyId);
            if (error is not null) return AccessResult.Fail(error);

            var email = (input.Email ?? string.Empty).Trim();
            var name = (input.FullName ?? string.Empty).Trim();
            if (name.Length is 0 or > 150) return AccessResult.Fail("Enter the person's full name (up to 150 characters).");
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Length > 256) return AccessResult.Fail("Enter a valid email address.");
            if (await _users.FindByEmailAsync(email) is not null) return AccessResult.Fail("An account with this email already exists.");

            var user = new AppUser
            {
                UserName = email,
                Email = email,
                PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim(),
                FullName = name,
                AccountType = scope == PermissionScope.Platform ? AccountType.PlatformStaff : AccountType.CompanyStaff,
                HousingCompanyId = companyId,
                LockoutEnabled = true,
            };

            // Roles are checked before the account exists, so a refused role leaves nothing behind.
            var roles = await AssignableRoles(user).Include(r => r.Permissions).Where(r => input.RoleIds.Contains(r.Id)).ToListAsync();
            if (roles.Count != input.RoleIds.Distinct().Count()) return AccessResult.Fail("One of the chosen roles belongs to another company or domain.");
            var refused = roles.FirstOrDefault(r => !MayGrant(actor, scope, r.Permissions.Select(p => p.PermissionKey)));
            if (refused is not null) return AccessResult.Fail($"You can't give the role \"{refused.Name}\": it contains permissions you don't have yourself.");

            var created = string.IsNullOrEmpty(input.Password) ? await _users.CreateAsync(user) : await _users.CreateAsync(user, input.Password);
            if (!created.Succeeded) return AccessResult.Fail(string.Join(" ", created.Errors.Select(e => e.Description)));

            foreach (var role in roles) _db.UserAccessRoles.Add(new UserAccessRole { UserId = user.Id, RoleId = role.Id, AssignedBy = ActorName(actor), AssignedOn = DateTime.UtcNow });
            _audit.Add(AuditCategories.Users, "UserCreated",
                $"{(scope == PermissionScope.Platform ? "Platform" : "Company")} user {name} ({email}) created{(roles.Count > 0 ? " with roles " + string.Join(", ", roles.Select(r => r.Name)) : "")}.",
                companyId, nameof(AppUser), user.Id, new { name, email, Roles = roles.Select(r => r.Name) });
            await _db.SaveChangesAsync();
            AccessControlService.Invalidate();
            return AccessResult.Ok($"{name}'s account was created.", user.Id);
        }

        public async Task<AccessResult> UpdateStaffUserAsync(UserAccess actor, string userId, string fullName, string? phoneNumber)
        {
            if (!actor.Has(Perm.UsersManage)) return AccessResult.Fail("You don't have permission to edit users.");
            var user = await VisibleStaff(actor).FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return AccessResult.Fail("That user doesn't exist or isn't yours to manage.");
            var name = (fullName ?? string.Empty).Trim();
            if (name.Length is 0 or > 150) return AccessResult.Fail("Enter the person's full name (up to 150 characters).");

            var before = new { user.FullName, user.PhoneNumber };
            user.FullName = name;
            user.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
            user.UpdatedOn = DateTime.Now;
            user.UpdatedBy = ActorName(actor);
            _audit.Add(AuditCategories.Users, "UserUpdated", $"Details of {name} ({user.Email}) changed.", user.HousingCompanyId, nameof(AppUser), user.Id,
                new { Before = before, After = new { user.FullName, user.PhoneNumber } });
            await _db.SaveChangesAsync();
            return AccessResult.Ok($"{name}'s details were saved.");
        }

        public async Task<AccessResult> SetUserRolesAsync(UserAccess actor, string userId, IReadOnlyCollection<Guid> roleIds)
        {
            if (!actor.Has(Perm.RolesAssign)) return AccessResult.Fail("You don't have permission to assign roles.");
            var user = await VisibleStaff(actor).FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return AccessResult.Fail("That user doesn't exist or isn't yours to manage.");
            if (user.Id == actor.UserId) return AccessResult.Fail("You can't change your own roles. Ask another administrator.");

            var scope = user.AccountType == AccountType.PlatformStaff ? PermissionScope.Platform : PermissionScope.Company;
            var wanted = await AssignableRoles(user).Include(r => r.Permissions).Where(r => roleIds.Contains(r.Id)).ToListAsync();
            if (wanted.Count != roleIds.Distinct().Count()) return AccessResult.Fail("One of the chosen roles belongs to another company or domain.");

            var current = await _db.UserAccessRoles.Include(ur => ur.Role).ThenInclude(r => r.Permissions).Where(ur => ur.UserId == userId).ToListAsync();
            var added = wanted.Where(r => current.All(c => c.RoleId != r.Id)).ToList();
            var removed = current.Where(c => wanted.All(r => r.Id != c.RoleId)).ToList();
            if (added.Count == 0 && removed.Count == 0) return AccessResult.Ok("Nothing changed.");

            var refused = added.FirstOrDefault(r => !MayGrant(actor, scope, r.Permissions.Select(p => p.PermissionKey)));
            if (refused is not null) return AccessResult.Fail($"You can't give the role \"{refused.Name}\": it contains permissions you don't have yourself.");

            foreach (var gone in removed.Where(r => IsSuperAdminRole(r.Role)))
            {
                if (IsActive(user) && !await AnotherActiveHolderAsync(gone.RoleId, user.Id))
                {
                    return AccessResult.Fail($"{DisplayName(user)} is the last active holder of \"{gone.Role.Name}\". Give it to someone else first.");
                }
            }

            _db.UserAccessRoles.RemoveRange(removed);
            foreach (var role in added) _db.UserAccessRoles.Add(new UserAccessRole { UserId = user.Id, RoleId = role.Id, AssignedBy = ActorName(actor), AssignedOn = DateTime.UtcNow });
            _audit.Add(AuditCategories.Users, "UserRolesChanged",
                $"Roles of {DisplayName(user)} changed.{(added.Count > 0 ? " Added: " + string.Join(", ", added.Select(r => r.Name)) + "." : "")}{(removed.Count > 0 ? " Removed: " + string.Join(", ", removed.Select(r => r.Role.Name)) + "." : "")}",
                user.HousingCompanyId, nameof(AppUser), user.Id, new { Added = added.Select(r => r.Name), Removed = removed.Select(r => r.Role.Name) });
            await _db.SaveChangesAsync();
            AccessControlService.Invalidate();
            return AccessResult.Ok($"{DisplayName(user)}'s roles were saved.");
        }

        public async Task<AccessResult> SetUserActiveAsync(UserAccess actor, string userId, bool active)
        {
            if (!actor.Has(Perm.UsersDeactivate)) return AccessResult.Fail("You don't have permission to deactivate users.");
            var user = await VisibleStaff(actor).FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return AccessResult.Fail("That user doesn't exist or isn't yours to manage.");
            if (user.Id == actor.UserId) return AccessResult.Fail("You can't deactivate your own account.");
            if (IsActive(user) == active) return AccessResult.Ok(active ? "The account is already active." : "The account is already deactivated.");

            if (!active)
            {
                var superRoles = await _db.UserAccessRoles.Include(ur => ur.Role).Where(ur => ur.UserId == userId).Select(ur => ur.Role).ToListAsync();
                foreach (var role in superRoles.Where(IsSuperAdminRole))
                {
                    if (!await AnotherActiveHolderAsync(role.Id, user.Id))
                        return AccessResult.Fail($"{DisplayName(user)} is the last active holder of \"{role.Name}\" and can't be deactivated.");
                }
            }

            user.LockoutEnabled = true;
            user.LockoutEnd = active ? null : DateTimeOffset.MaxValue;
            await _users.UpdateSecurityStampAsync(user); // signs the person out everywhere within a minute (also saves the lockout)
            _audit.Add(AuditCategories.Users, active ? "UserReactivated" : "UserDeactivated",
                $"{DisplayName(user)} ({user.Email}) was {(active ? "reactivated" : "deactivated")}.", user.HousingCompanyId, nameof(AppUser), user.Id);
            await _db.SaveChangesAsync();
            AccessControlService.Invalidate();
            return AccessResult.Ok($"{DisplayName(user)} was {(active ? "reactivated" : "deactivated")}.");
        }

        // ---------- Helpers ----------

        public static bool IsActive(AppUser user) => !(user.LockoutEnabled && user.LockoutEnd is DateTimeOffset end && end > DateTimeOffset.UtcNow);

        public static string DisplayName(AppUser user) =>
            !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName!
            : user.Customer is { } p && !string.IsNullOrWhiteSpace($"{p.FirstName} {p.LastName}".Trim()) ? $"{p.FirstName} {p.LastName}".Trim()
            : user.Email ?? user.UserName ?? "Unknown";

        /// <summary>Platform staff acting on a company is oversight; otherwise you only grant what you hold.</summary>
        private static bool MayGrant(UserAccess actor, PermissionScope targetScope, IEnumerable<string> keys) =>
            (actor.IsPlatform && targetScope == PermissionScope.Company) || keys.All(actor.Has);

        private async Task<bool> AnotherActiveHolderAsync(Guid roleId, string exceptUserId)
        {
            var now = DateTimeOffset.UtcNow;
            return await _db.UserAccessRoles.AnyAsync(ur => ur.RoleId == roleId && ur.UserId != exceptUserId
                && !(ur.User.LockoutEnabled && ur.User.LockoutEnd != null && ur.User.LockoutEnd > now));
        }

        private async Task<(PermissionScope Scope, Guid? CompanyId, string? Error)> TargetDomainAsync(UserAccess actor, Guid? requestedCompanyId)
        {
            if (actor.IsPlatform)
            {
                if (requestedCompanyId is null) return (PermissionScope.Platform, null, null);
                var exists = await _db.HousingCompanies.AnyAsync(c => c.CompanyId == requestedCompanyId);
                return exists ? (PermissionScope.Company, requestedCompanyId, null) : (PermissionScope.Company, null, "That company doesn't exist.");
            }
            if (actor.IsCompanyStaff && actor.CompanyId is Guid own) return (PermissionScope.Company, own, null);
            return (PermissionScope.Company, null, "Your account isn't staff of a company.");
        }

        private async Task<string?> ValidateRoleAsync(UserAccess actor, PermissionScope scope, Guid? companyId, string name, Guid? roleId,
            IEnumerable<string> permissions, IEnumerable<string> newlyGranted)
        {
            if (name.Length is 0 or > 100) return "Give the role a name (up to 100 characters).";
            var taken = await _db.AccessRoles.AnyAsync(r => r.CompanyId == companyId && r.Name == name && r.Id != roleId);
            if (taken) return $"There is already a role called \"{name}\".";
            foreach (var key in permissions)
            {
                var def = PermissionCatalog.Find(key);
                if (def is null) return $"Unknown permission \"{key}\".";
                if (!PermissionCatalog.AllowedIn(def, scope)) return $"\"{def.Name}\" can't be part of a {(scope == PermissionScope.Platform ? "platform" : "company")} role.";
            }
            if (!MayGrant(actor, scope, newlyGranted))
            {
                var missing = newlyGranted.Where(k => !actor.Has(k)).Select(k => PermissionCatalog.Find(k)?.Name ?? k);
                return "You can only add permissions you have yourself. Not yours: " + string.Join(", ", missing) + ".";
            }
            return null;
        }

        private static string? ActorName(UserAccess actor) => actor.UserId;
    }
}
