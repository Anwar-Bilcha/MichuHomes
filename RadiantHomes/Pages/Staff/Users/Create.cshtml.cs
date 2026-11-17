using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Security;
using RadiantHomes.Services.Email;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Staff.Users
{
    /// <summary>
    /// Creates a staff account: platform staff create platform users or (onboarding/oversight) a company's users;
    /// company staff create their own company's users. Roles are limited to what the creator may give.
    /// </summary>
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly AccessManagementService _manage;
        private readonly AccountEmails _emails;
        private readonly UserManager<AppUser> _users;

        public CreateModel(ApplicationDbContext db, AccessManagementService manage, AccountEmails emails, UserManager<AppUser> users)
        {
            _db = db;
            _manage = manage;
            _emails = emails;
            _users = users;
        }

        /// <summary>Platform staff: the company the new user belongs to (none: a platform user).</summary>
        [BindProperty(SupportsGet = true)] public Guid? Company { get; set; }
        [BindProperty] public InputModel Input { get; set; } = new();

        public UserAccess Access { get; private set; } = UserAccess.Anonymous;
        public List<SelectListItem> CompanyOptions { get; private set; } = new();
        public string DomainLabel { get; private set; } = string.Empty;
        public List<RoleOption> Roles { get; private set; } = new();
        public bool CanEmail => _emails.CanSend;

        public sealed record RoleOption(Guid Id, string Name, string? Description, bool IsSystem, int PermissionCount, bool CanGive, bool Active);

        public class InputModel
        {
            [Required, StringLength(150), Display(Name = "Full name")]
            public string FullName { get; set; } = string.Empty;
            [Required, EmailAddress, StringLength(256)]
            public string Email { get; set; } = string.Empty;
            [Phone, Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }
            /// <summary>"invite" (email a link to choose a password) or "password".</summary>
            public string Setup { get; set; } = "invite";
            [DataType(DataType.Password), Display(Name = "First password")]
            public string? Password { get; set; }
            public List<Guid> RoleIds { get; set; } = new();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            if (!CanEmail) Input.Setup = "password";
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadAsync();
            var invite = Input.Setup == "invite" && CanEmail;
            if (!invite && string.IsNullOrEmpty(Input.Password))
            {
                ModelState.AddModelError("Input.Password", CanEmail ? "Enter a first password, or choose to email an invitation." : "Enter a first password (emails aren't set up on this server).");
            }
            if (!ModelState.IsValid) return Page();

            var result = await _manage.CreateStaffUserAsync(Access, new StaffUserInput
            {
                FullName = Input.FullName,
                Email = Input.Email,
                PhoneNumber = Input.PhoneNumber,
                Password = invite ? null : Input.Password,
                CompanyId = Access.IsPlatform ? Company : null,
                RoleIds = Input.RoleIds,
            });
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return Page();
            }

            var message = result.Message;
            if (invite && await _users.FindByIdAsync(result.Id!) is { } created)
            {
                try
                {
                    await _emails.SendPasswordLinkAsync(created, invitation: true);
                    message += $" An invitation was emailed to {created.Email}.";
                }
                catch
                {
                    message += " The invitation email could not be sent; use \"Send password link\" to try again.";
                }
            }
            TempData["Success"] = message;
            return RedirectToPage("./Edit", new { id = result.Id });
        }

        private async Task LoadAsync()
        {
            Access = await HttpContext.AccessAsync();
            Guid? companyId = Access.IsPlatform ? Company : Access.CompanyId;
            if (Access.IsPlatform)
            {
                var companies = await _db.HousingCompanies.AsNoTracking().OrderBy(c => c.Name).Select(c => new { c.CompanyId, c.Name }).ToListAsync();
                CompanyOptions = new List<SelectListItem> { new("Michu Homes (platform staff)", "") };
                CompanyOptions.AddRange(companies.Select(c => new SelectListItem(c.Name, c.CompanyId.ToString())));
            }
            DomainLabel = companyId is Guid id
                ? await _db.HousingCompanies.Where(c => c.CompanyId == id).Select(c => c.Name).FirstOrDefaultAsync() ?? "Company"
                : "Michu Homes (platform staff)";

            var probe = new AppUser
            {
                AccountType = companyId is null ? AccountType.PlatformStaff : AccountType.CompanyStaff,
                HousingCompanyId = companyId,
            };
            var roles = await _manage.AssignableRoles(probe).AsNoTracking().Include(r => r.Permissions).OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name).ToListAsync();
            var oversight = Access.IsPlatform && companyId is not null;
            Roles = roles.Select(r => new RoleOption(r.Id, r.Name, r.Description, r.IsSystem, r.Permissions.Count,
                oversight || r.Permissions.All(p => Access.Has(p.PermissionKey)), r.IsActive)).ToList();
        }
    }
}
