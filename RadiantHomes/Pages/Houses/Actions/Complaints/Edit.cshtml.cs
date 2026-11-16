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
    public class EditModel : PageModel
    {
        private readonly RadiantHomes.DAL.ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<AppUser> _userManager;

        public EditModel(RadiantHomes.DAL.ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, UserManager<AppUser> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }

        /// <summary>The owner of the complaint's house (shown read-only; it always comes from the house).</summary>
        public string OwnerName { get; private set; } = string.Empty;

        [BindProperty]
        public IssueOrCompliant IssueOrCompliant { get; set; } = default!;

        [BindProperty, Display(Name = "Supporting evidence")]
        public IFormFile? SupportingEvidence { get; set; }

        [BindProperty, Display(Name = "Additional evidence")]
        public IFormFile? SupportingEvidence2 { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var issueorcompliant =  await _context.IssuesOrCompliants.AsNoTracking().FirstOrDefaultAsync(m => m.IssueId == id);
            if (issueorcompliant == null)
            {
                return NotFound();
            }
            // Only the complainant or a Michu admin may change a complaint (see ComplaintAccess).
            if (!ComplaintAccess.CanChange(User, await HouseAccess.CurrentCustomerIdAsync(_userManager, User), issueorcompliant))
            {
                return Forbid();
            }
            IssueOrCompliant = issueorcompliant;
            await LoadSelectListsAsync();
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            var existing = await _context.IssuesOrCompliants.FirstOrDefaultAsync(m => m.IssueId == IssueOrCompliant.IssueId);
            if (existing == null)
            {
                return NotFound();
            }
            if (!ComplaintAccess.CanChange(User, await HouseAccess.CurrentCustomerIdAsync(_userManager, User), existing))
            {
                return Forbid();
            }

            // The house owner always comes from the (possibly changed) house; never trust a posted owner id.
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

            HouseFormValidation.IgnoreNavigationProperties(ModelState, nameof(IssueOrCompliant),
                nameof(IssueOrCompliant.House), nameof(IssueOrCompliant.HouseOwner));
            UploadedFiles.Validate(ModelState, nameof(SupportingEvidence), SupportingEvidence, UploadedFiles.DocumentExtensions);
            UploadedFiles.Validate(ModelState, nameof(SupportingEvidence2), SupportingEvidence2, UploadedFiles.DocumentExtensions);
            if (!ModelState.IsValid)
            {
                // The evidence links aren't posted back; show the stored ones again.
                IssueOrCompliant.SupportingEvidenceUrl = existing.SupportingEvidenceUrl;
                IssueOrCompliant.SupportingEvidence2Url = existing.SupportingEvidence2Url;
                await LoadSelectListsAsync();
                return Page();
            }

            existing.IssueName = IssueOrCompliant.IssueName;
            existing.HouseId = IssueOrCompliant.HouseId;
            existing.HouseOwnerId = IssueOrCompliant.HouseOwnerId;
            existing.IssueCategory = IssueOrCompliant.IssueCategory;
            existing.DetailedDescription = IssueOrCompliant.DetailedDescription;
            existing.SeverityLevel = IssueOrCompliant.SeverityLevel;
            existing.ComplainantFullName = IssueOrCompliant.ComplainantFullName;
            existing.HowDoWeReachYou = IssueOrCompliant.HowDoWeReachYou;
            existing.PhoneOrEmail = IssueOrCompliant.PhoneOrEmail;
            existing.ComplainantAddress = IssueOrCompliant.ComplainantAddress;

            // Replace an evidence file only when a new one was uploaded; otherwise keep the stored link.
            var webRoot = _webHostEnvironment.WebRootPath;
            var folder = $"uploads/complaints/{existing.IssueId}";
            if (SupportingEvidence is { Length: > 0 })
            {
                UploadedFiles.DeleteIfExists(webRoot, existing.SupportingEvidenceUrl);
                existing.SupportingEvidenceUrl = await UploadedFiles.SaveAsync(SupportingEvidence, webRoot, folder, "Evidence1");
            }
            if (SupportingEvidence2 is { Length: > 0 })
            {
                UploadedFiles.DeleteIfExists(webRoot, existing.SupportingEvidence2Url);
                existing.SupportingEvidence2Url = await UploadedFiles.SaveAsync(SupportingEvidence2, webRoot, folder, "Evidence2");
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!IssueOrCompliantExists(IssueOrCompliant.IssueId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            TempData["Success"] = "Complaint updated.";
            return RedirectToPage("./Index");
        }

        private async Task LoadSelectListsAsync()
        {
            ViewData["HouseId"] = HouseSelectLists.Houses(_context, IssueOrCompliant.HouseId);
            OwnerName = await DisplayText.PersonNameAsync(_context, IssueOrCompliant.HouseOwnerId);
        }

        private bool IssueOrCompliantExists(Guid id)
        {
            return _context.IssuesOrCompliants.Any(e => e.IssueId == id);
        }
    }
}
