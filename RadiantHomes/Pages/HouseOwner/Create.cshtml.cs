using Microsoft.AspNetCore.Authorization;
using RadiantHomes.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Shared;
using RadiantHomes.Services.NotificationService;
using RadiantHomes.Utilities;
using System.IO;
using System.Security.Claims; // For User.IsInRole
using System; // For Guid

namespace RadiantHomes.Pages.HouseOwner
{
    // Authorize this page based on your overall policy. If it's a general customer registration,
    // you might not need an [Authorize] attribute here, or a less restrictive one.
    // For DinarAdmin, we'll check the role explicitly inside OnGet/OnPost.

    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notificationService;

        public CreateModel(ApplicationDbContext context, IWebHostEnvironment env, INotificationService notificationService)
        {
            _context = context;
            _env = env;
            _notificationService = notificationService;
        }

        [BindProperty]
        public Models.Customer Customer { get; set; }

        [BindProperty]
        public PartyAddress NewAddress { get; set; }

        [BindProperty]
        public IFormFile? IdCardFile { get; set; }

        [BindProperty]
        public IFormFile? CustomerPhoto { get; set; }

        public SelectList Rankings { get; set; }
        public SelectList AddressOptions { get; set; }
        public string ErrorMessage { get; set; }

        // Parameters for Dinar Admin scenario
        // userId here is the proposed ContactPersonId (Guid as per your HousingCompany.ContactPersonId)
        // This is not the Identity user's string ID yet.
        [BindProperty(SupportsGet = true)] // Allow binding from URL for GET requests
        public Guid? UserId { get; set; }

        [BindProperty(SupportsGet = true)] // Allow binding from URL for GET requests
        public Guid? CompanyId { get; set; }

        public bool IsDinarAdminPrecreationMode { get; set; } = false;


        public async Task<IActionResult> OnGetAsync()
        {
            ErrorMessage = TempData["ErrorMessage"] as string;
            TempData.Keep("ErrorMessage");

            // Check if current user is DinarAdmin AND parameters are provided for the special flow
            if (User.HasPermission(Perm.CompaniesCreate) && UserId.HasValue && CompanyId.HasValue)
            {
                IsDinarAdminPrecreationMode = true;

                // Validate that the TargetCompanyId exists
                var housingCompany = await _context.HousingCompanies
                                                   .FirstOrDefaultAsync(hc => hc.CompanyId == CompanyId.Value);

                if (housingCompany == null)
                {
                    ErrorMessage = "Invalid Company ID provided for Real Estate Admin profile pre-creation.";
                    // Reset parameters so the page doesn't try special mode again
                    UserId = null;
                    CompanyId = null;
                    IsDinarAdminPrecreationMode = false;
                }
                else
                {
                    // For pre-creation, ensure no profile already exists for this proposed ID
                    var existingProfile = await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == UserId.Value);
                    if (existingProfile != null)
                    {
                        ErrorMessage = $"A customer profile already exists for the proposed Contact Person ID: {UserId.Value}. Cannot pre-create.";
                        // Reset parameters
                        UserId = null;
                        CompanyId = null;
                        IsDinarAdminPrecreationMode = false;
                    }
                    else
                    {
                        // Pre-fill Customer model with relevant info for Dinar Admin review
                        Customer = new Models.Customer
                        {
                            // ID will be set from ProposedContactPersonId on Post
                            CompanyId = CompanyId.Value,
                            ProfileStatus = "Approved", // Dinar Admin pre-creates as approved
                            ApplicationDate = DateTime.Now
                        };
                        // Optionally fetch existing user details if any, but per spec, this is a new profile
                        // and the Identity user is created *after* this.
                    }
                }
            }

            // Defaults for the "+ Add New Address" fields.
            NewAddress ??= new PartyAddress { Country = "Ethiopia" };
            await PopulateSelectLists();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Re-check the Dinar Admin pre-creation mode on POST for security and consistent behavior
            IsDinarAdminPrecreationMode = User.HasPermission(Perm.CompaniesCreate) && UserId.HasValue && CompanyId.HasValue;

            // Reject unsupported or oversized uploads before anything is saved.
            string? uploadField = null, uploadMessage = null;
            if (IdCardFile != null && SafeUpload.Validate(IdCardFile, SafeUpload.DocumentExtensions) is string idError)
            {
                uploadField = nameof(IdCardFile);
                uploadMessage = idError;
            }
            else if (CustomerPhoto != null && SafeUpload.Validate(CustomerPhoto, SafeUpload.ImageExtensions) is string photoError)
            {
                uploadField = nameof(CustomerPhoto);
                uploadMessage = photoError;
            }
            if (uploadMessage != null)
            {
                ModelState.AddModelError(uploadField!, uploadMessage);
                ErrorMessage = uploadMessage;
                await PopulateSelectLists();
                return Page();
            }

            // --- Address Handling ---
            if (Request.Form["Customer.AddressId"] == "new")
            {
                // Validate only the new-address fields: the "new" marker itself never binds to the Guid
                // Customer.AddressId, and the rest of Customer isn't validated on this page either.
                if (NewAddress == null || ModelState.GetFieldValidationState(nameof(NewAddress)) == ModelValidationState.Invalid)
                {
                    ErrorMessage = "Please correct new address details.";
                    // Re-populate select lists if validation fails
                    await PopulateSelectLists();
                    return Page();
                }
                // Generate the key here: the model declares a NEWID() default for AddressID but the
                // database has no such default, so leaving it to SQL Server inserts NULL.
                NewAddress.AddressID = Guid.NewGuid();
                _context.Addresses.Add(NewAddress);
                await _context.SaveChangesAsync();
                Customer.AddressId = NewAddress.AddressID;
            }
            else if (Customer.AddressId == Guid.Empty) // If no existing address was selected and no new one
            {
                ModelState.AddModelError("Customer.AddressId", "Please select an existing address or create a new one.");
                await PopulateSelectLists();
                return Page();
            }

            // --- Default Profile Values ---
            Customer.ProfileStatus = "Pending"; // Default for normal customers

            // Ensure RankingId is set (assuming a default exists or is chosen)
            if (Customer.RankingId == Guid.Empty)
            {
                var defaultRanking = await _context.HomeOwnerRankings.FirstOrDefaultAsync();
                if (defaultRanking != null)
                {
                    Customer.RankingId = defaultRanking.RankingId;
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "No default ranking found. Please configure rankings.");
                    await PopulateSelectLists();
                    return Page();
                }
            }
            Customer.ApplicationDate = DateTime.Now;

            // --- Dinar Admin Pre-creation Logic ---
            if (IsDinarAdminPrecreationMode)
            {
                // Verify the company exists and get its details
                var housingCompany = await _context.HousingCompanies
                                                   .FirstOrDefaultAsync(hc => hc.CompanyId == CompanyId.Value);

                if (housingCompany == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid Company ID specified for pre-creation.");
                    await PopulateSelectLists();
                    return Page();
                }

                // Set Customer.Id to the ProposedContactPersonId (which will be the Identity user's ID later)
                Customer.Id = UserId.Value;
                Customer.CompanyId = CompanyId.Value; // Assign the company to this profile
                Customer.ProfileStatus = "Approved"; // Dinar Admin pre-approves this profile

                // Crucial check: Ensure a profile with this ID doesn't already exist
                var existingProfile = await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == Customer.Id);
                if (existingProfile != null)
                {
                    ModelState.AddModelError(string.Empty, $"A customer profile with ID '{Customer.Id}' already exists. Cannot pre-create.");
                    await PopulateSelectLists();
                    return Page();
                }

                _context.UserProfiles.Add(Customer);
                await _context.SaveChangesAsync();

                // Now, update HousingCompany.ContactPersonId to the newly created profile's ID
                // This links the company to this specific user profile.
               // housingCompany.ContactPerson = Customer.Id; // This is the string ID of the Customer/UserProfile
                //_context.HousingCompanies.Update(housingCompany);
                await _context.SaveChangesAsync();

                // Redirect to a confirmation page or company details page for the Dinar Admin
                TempData["SuccessMessage"] = $"Real Estate Admin profile for '{Customer.FirstName} {Customer.LastName}' pre-created and assigned to company '{housingCompany.Name}'. Please proceed to register the user's account with this ID to complete the setup.";
                // You might redirect Dinar Admin to the user list or company details page
                // Register links the new account to this profile and company (it reads customerId / companyId).
                return RedirectToPage("/Account/Register", new { area = "Identity", customerId = Customer.Id, companyId = CompanyId.Value });
                // Or if you need to prompt for registration:
                // return Redirect($"/Identity/Account/Register?customerId={Customer.Id}"); // Pass the Customer.Id as the Identity UserId
            }
            // --- Normal User/Customer Profile Creation Logic ---
            else
            {
                // This is the flow for a new customer registering themselves.
                // Generate the key here: the model declares a NEWID() default for UserProfiles.Id but the
                // database has no such default, so leaving it to SQL Server inserts NULL.
                Customer.Id = Guid.NewGuid();
                _context.UserProfiles.Add(Customer);
                await _context.SaveChangesAsync();

                var customerId = Customer.Id; // Get the newly generated ID

                // Handle file uploads (existing logic)
                // Note: File uploads for Dinar Admin pre-creation are typically handled separately,
                // perhaps through an admin interface after the user is registered.
                // File names are fixed ("id-card", "photo") rather than built from the person's name,
                // so user input can never shape the path. ID documents are not served as static files
                // (see the /uploads/ids block in Program.cs); they're downloaded via HouseOwner/IdDocument.
                if (IdCardFile != null)
                {
                    var idFileName = "id-card" + SafeUpload.AllowedExtension(IdCardFile, SafeUpload.DocumentExtensions);
                    var idPath = SafeUpload.Under(Path.Combine(_env.WebRootPath, "uploads", "ids"), customerId.ToString());
                    Directory.CreateDirectory(idPath);
                    using var stream = new FileStream(Path.Combine(idPath, idFileName), FileMode.Create);
                    await IdCardFile.CopyToAsync(stream);
                    Customer.IdCardFileUrl = $"/uploads/ids/{customerId}/{idFileName}";
                }

                if (CustomerPhoto != null)
                {
                    var photoFileName = "photo" + SafeUpload.AllowedExtension(CustomerPhoto, SafeUpload.ImageExtensions);
                    var photoPath = SafeUpload.Under(Path.Combine(_env.WebRootPath, "uploads", "photos"), customerId.ToString());
                    Directory.CreateDirectory(photoPath);
                    using var stream = new FileStream(Path.Combine(photoPath, photoFileName), FileMode.Create);
                    await CustomerPhoto.CopyToAsync(stream);
                    Customer.CustomerPhotoUrl = $"/uploads/photos/{customerId}/{photoFileName}";
                }
                
                    
                await _context.SaveChangesAsync(); // Save file paths to DB
                Notification notification = new Notification { CreatedAt = DateTime.Now, DataKey = Customer.Id.ToString(), IsRead = false, Message = $"{Customer.FirstName} {Customer.LastName} has registered and awaits approval.", Title = "New User Profile Pending Approval", ReceiverAdminId = CompanyId, RecipientRole = "Admin", ReceiverCompanyId = CompanyId,
                    // Notifications.Datatype is NOT NULL; without it the insert failed (silently, inside the service).
                    Datatype = EntityAffecteds.UserProfiles.ToString() };
                await _notificationService.CreateNotificationAsync(notification);

                TempData["CustomerId"] = Customer.Id.ToString(); // Pass the newly generated Customer.Id
                TempData["ApplicantName"] = $"{Customer.FirstName} {Customer.LastName}".Trim(); // greeting on HouseOwner/Success
                // Next: create the login for this profile, then land on the "application received" page.
                return RedirectToPage("/Account/Register", new
                {
                    area = "Identity",
                    companyId = Customer.CompanyId,
                    customerId = Customer.Id,
                    returnUrl = Url.Page("/HouseOwner/Success")
                });
            }
        }

        // Helper to re-populate select lists on validation failure
        private async Task PopulateSelectLists()
        {
            AddressOptions = new SelectList(await _context.Addresses.ToListAsync(), "AddressID","FormattedAddressCompact");
            Rankings = new SelectList(await _context.HomeOwnerRankings.ToListAsync(), "RankingId", "RankName");
        }
    }
}