using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Services.NotificationService;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>
    /// The customer profile of the signed-in account: created here when it is missing (reserving, commenting and
    /// referring need it) and edited afterwards. A new profile waits for approval like one made on HouseOwner/Create.
    /// </summary>
    public class ProfileModel : PortalPageModel
    {
        [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
        [BindProperty] public ProfileInput Input { get; set; } = new();

        public sealed class ProfileInput
        {
            [Required(ErrorMessage = "First name is required.")]
            [StringLength(60, ErrorMessage = "First name can be at most 60 characters.")]
            [Display(Name = "First name")]
            public string FirstName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Last name is required.")]
            [StringLength(60, ErrorMessage = "Last name can be at most 60 characters.")]
            [Display(Name = "Last name")]
            public string LastName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Phone number is required.")]
            [RegularExpression(@"^\+?[0-9 ()\-]{9,20}$", ErrorMessage = "Please enter a valid phone number.")]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; } = string.Empty;

            [Required(ErrorMessage = "City is required.")]
            [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
            public string City { get; set; } = "Addis Ababa";

            [StringLength(100)]
            [Display(Name = "Sub-city")]
            public string? SubCity { get; set; }

            [StringLength(100)]
            public string? Woreda { get; set; }

            [Required(ErrorMessage = "Street or area is required.")]
            [StringLength(200, ErrorMessage = "Street cannot exceed 200 characters.")]
            [Display(Name = "Street or area")]
            public string Street { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (Me.Profile is { } p)
            {
                Input = new ProfileInput
                {
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    PhoneNumber = p.PhoneNumber ?? string.Empty,
                    City = p.Address?.City ?? "Addis Ababa",
                    SubCity = p.Address?.SubCity,
                    Woreda = p.Address?.Woreda,
                    Street = p.Address?.Street ?? string.Empty,
                };
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (await LoadMeAsync() is { } problem) return problem;
            if (!ModelState.IsValid) return Page();

            var context = Service<ApplicationDbContext>();
            if (Me.Profile is { } existing)
            {
                var profile = await context.UserProfiles.Include(c => c.Address).FirstAsync(c => c.Id == existing.Id);
                profile.FirstName = Input.FirstName.Trim();
                profile.LastName = Input.LastName.Trim();
                profile.PhoneNumber = Input.PhoneNumber.Trim();
                if (profile.Address is { } address)
                {
                    Fill(address);
                }
                else
                {
                    var created = NewAddress();
                    context.Addresses.Add(created);
                    profile.AddressId = created.AddressID;
                }
                await context.SaveChangesAsync();
                TempData["Success"] = "Your profile was saved.";
                return Back();
            }

            var newAddress = NewAddress();
            context.Addresses.Add(newAddress);
            var ranking = await context.HomeOwnerRankings.Select(r => r.RankingId).FirstOrDefaultAsync();
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                FirstName = Input.FirstName.Trim(),
                LastName = Input.LastName.Trim(),
                PhoneNumber = Input.PhoneNumber.Trim(),
                Email = Me.Account.Email,
                AddressId = newAddress.AddressID,
                RankingId = ranking,
                CustomerCategory = "Individual",
                ProfileStatus = "Pending",
                ApplicationDate = DateTime.Now,
            };
            context.UserProfiles.Add(customer);
            await context.SaveChangesAsync();

            Me.Account.CustomerId = customer.Id;
            await Service<UserManager<AppUser>>().UpdateAsync(Me.Account);

            // The same notice HouseOwner/Create sends for a new profile.
            await Service<INotificationService>().CreateNotificationAsync(new Notification
            {
                CreatedAt = DateTime.Now,
                DataKey = customer.Id.ToString(),
                IsRead = false,
                Message = $"{customer.FirstName} {customer.LastName} has registered and awaits approval.",
                Title = "New User Profile Pending Approval",
                RecipientRole = "Admin",
                Datatype = EntityAffecteds.UserProfiles.ToString(),
                RedirectUrl = $"/HouseOwner/Details?id={customer.Id}",
            });

            TempData["Success"] = "Your profile was created. You can now reserve homes, comment and refer friends.";
            return Back();
        }

        private PartyAddress NewAddress()
        {
            var address = new PartyAddress { AddressID = Guid.NewGuid(), Country = "Ethiopia" };
            Fill(address);
            return address;
        }

        private void Fill(PartyAddress address)
        {
            address.City = Input.City.Trim();
            address.SubCity = string.IsNullOrWhiteSpace(Input.SubCity) ? null : Input.SubCity.Trim();
            address.Woreda = string.IsNullOrWhiteSpace(Input.Woreda) ? null : Input.Woreda.Trim();
            address.Street = Input.Street.Trim();
            if (string.IsNullOrWhiteSpace(address.Country)) address.Country = "Ethiopia";
        }

        private IActionResult Back() =>
            !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : RedirectToPage("/Portal/Index");
    }
}
