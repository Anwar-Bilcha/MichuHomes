using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.Houses.Actions.Complaints
{
    public class CreateModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<AppUser> _userManager;

        public CreateModel(RadiantHomes.DAL.ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, UserManager<AppUser> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }

        /// <summary>Michu admins may file a complaint on someone's behalf (they type the complainant's name).</summary>
        public bool IsAdmin { get; private set; }

        /// <summary>The owner of the chosen house (the complaint's owner always comes from the house).</summary>
        public string OwnerName { get; private set; } = string.Empty;

        /// <summary>Set when the signed-in user owns the house: owners can't report their own house.</summary>
        public string? OwnHouseMessage { get; private set; }

        /// <param name="id">Optional house id (the "Report" button on the house details page passes it).</param>
        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            IsAdmin = TransactionAccess.IsAdmin(User);
            IssueOrCompliant = new IssueOrCompliant();
            if (id is Guid houseId)
            {
                var house = await _context.Houses.AsNoTracking().FirstOrDefaultAsync(h => h.Id == houseId);
                if (house is not null)
                {
                    IssueOrCompliant.HouseId = house.Id;
                    IssueOrCompliant.HouseOwnerId = house.HouseOwnerId;
                    if (!IsAdmin && await HouseAccess.CurrentCustomerIdAsync(_userManager, User) == house.HouseOwnerId)
                    {
                        OwnHouseMessage = "This is your own house, so you can't report it.";
                        return Page();
                    }
                }
            }
            if (!IsAdmin)
            {
                IssueOrCompliant.ComplainantFullName = await ComplainantNameAsync();
            }
            await LoadFormDataAsync();
            return Page();
        }

        [BindProperty]
        public IssueOrCompliant IssueOrCompliant { get; set; } = default!;

        [BindProperty, Display(Name = "Supporting evidence")]
        public IFormFile? SupportingEvidence { get; set; }

        [BindProperty, Display(Name = "Additional evidence")]
        public IFormFile? SupportingEvidence2 { get; set; }

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            IsAdmin = TransactionAccess.IsAdmin(User);
            HouseFormValidation.IgnoreNavigationProperties(ModelState, nameof(IssueOrCompliant),
                nameof(IssueOrCompliant.House), nameof(IssueOrCompliant.HouseOwner));
            UploadedFiles.Validate(ModelState, nameof(SupportingEvidence), SupportingEvidence, UploadedFiles.DocumentExtensions);
            UploadedFiles.Validate(ModelState, nameof(SupportingEvidence2), SupportingEvidence2, UploadedFiles.DocumentExtensions);

            // The house owner always comes from the house; never trust a posted owner id.
            ModelState.Remove("IssueOrCompliant.HouseOwnerId");
            var house = await _context.Houses.AsNoTracking().FirstOrDefaultAsync(h => h.Id == IssueOrCompliant.HouseId);
            if (house is null)
            {
                ModelState.Remove("IssueOrCompliant.HouseId");
                ModelState.AddModelError("IssueOrCompliant.HouseId", "Please choose the house this complaint is about.");
            }
            else
            {
                IssueOrCompliant.HouseOwnerId = house.HouseOwnerId;
            }

            if (!IsAdmin)
            {
                // The complainant is the signed-in user, whatever name was posted.
                ModelState.Remove("IssueOrCompliant.ComplainantFullName");
                IssueOrCompliant.ComplainantFullName = await ComplainantNameAsync();
                if (house is not null && await HouseAccess.CurrentCustomerIdAsync(_userManager, User) == house.HouseOwnerId)
                {
                    ModelState.AddModelError("IssueOrCompliant.HouseId", "This is your own house, so you can't report it.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadFormDataAsync();
                return Page();
            }

            // Generate the id here: the database has no default for it (despite the NEWID() default in the EF model),
            // and the evidence files are stored under it.
            IssueOrCompliant.IssueId = Guid.NewGuid();
            var folder = $"uploads/complaints/{IssueOrCompliant.IssueId}";
            IssueOrCompliant.SupportingEvidenceUrl = await UploadedFiles.SaveAsync(SupportingEvidence, _webHostEnvironment.WebRootPath, folder, "Evidence1");
            IssueOrCompliant.SupportingEvidence2Url = await UploadedFiles.SaveAsync(SupportingEvidence2, _webHostEnvironment.WebRootPath, folder, "Evidence2");

            _context.IssuesOrCompliants.Add(IssueOrCompliant);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                UploadedFiles.DeleteFolder(_webHostEnvironment.WebRootPath, folder); // don't leave the files of an unsaved complaint behind
                throw;
            }

            if (IsAdmin)
            {
                TempData["Success"] = "Complaint saved.";
                return RedirectToPage("./Index");
            }
            // Complainants can't be recognised in the complaints list yet (no complainant column), so confirm on
            // the house page instead.
            TempData["Success"] = $"Thank you. Your complaint about \"{house!.Title}\" was sent to Michu Homes.";
            return RedirectToPage("/Houses/Details", new { id = house.Id });
        }

        /// <summary>The signed-in user's name: their customer profile's name, else the account name.</summary>
        private async Task<string?> ComplainantNameAsync()
        {
            var customerId = await HouseAccess.CurrentCustomerIdAsync(_userManager, User);
            var name = customerId is Guid id ? await DisplayText.PersonNameAsync(_context, id) : null;
            return string.IsNullOrWhiteSpace(name) ? User.Identity?.Name : name;
        }

        private async Task LoadFormDataAsync()
        {
            ViewData["HouseId"] = HouseSelectLists.Houses(_context, IssueOrCompliant?.HouseId);
            if (IssueOrCompliant is not null && IssueOrCompliant.HouseOwnerId != Guid.Empty)
            {
                OwnerName = await DisplayText.PersonNameAsync(_context, IssueOrCompliant.HouseOwnerId);
            }
        }
    }
}
