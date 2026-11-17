using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RadiantHomes.Models.Kyc;
using RadiantHomes.Services.Kyc;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Pages.Portal
{
    /// <summary>The customer's identity verification: status, the reviewer's message, and the submission form.</summary>
    [RequestSizeLimit(35 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 35 * 1024 * 1024)]
    public class KycModel : PortalPageModel
    {
        [BindProperty] public InputModel Input { get; set; } = new();

        public KycApplication? Latest { get; private set; }
        public string Status => Me.Profile?.KycStatus ?? KycStatuses.NotSubmitted;
        public bool CanSubmit => Me.HasProfile && KycStatuses.CanSubmit(Status);

        public class InputModel
        {
            [Display(Name = "Document type")]
            public string DocumentType { get; set; } = "NationalId";
            [Display(Name = "Document number")]
            public string DocumentNumber { get; set; } = string.Empty;
            [Display(Name = "Full name as on the document")]
            public string FullNameOnDocument { get; set; } = string.Empty;
            [Display(Name = "Date of birth"), DataType(DataType.Date)]
            public DateTime? DateOfBirth { get; set; }
            [Display(Name = "Expiry date (if printed)"), DataType(DataType.Date)]
            public DateTime? DocumentExpiresOn { get; set; }
            [Display(Name = "Front of the document")]
            public IFormFile? Front { get; set; }
            [Display(Name = "Back of the document (if it has one)")]
            public IFormFile? Back { get; set; }
            [Display(Name = "Photo of you holding the document")]
            public IFormFile? Selfie { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (await LoadMeAsync() is { } challenge) return challenge;
            if (!Me.HasProfile) return CompleteProfileFirst();
            Latest = await Service<KycService>().LatestAsync(Me.CustomerId!.Value);
            Input.FullNameOnDocument = $"{Me.Profile!.FirstName} {Me.Profile.LastName}".Trim();
            if (Latest is not null)
            {
                Input.DocumentType = Latest.DocumentType;
                Input.DocumentNumber = Latest.DocumentNumber;
                Input.FullNameOnDocument = Latest.FullNameOnDocument;
                Input.DateOfBirth = Latest.DateOfBirth;
                Input.DocumentExpiresOn = Latest.DocumentExpiresOn;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (await LoadMeAsync() is { } challenge) return challenge;
            if (!Me.HasProfile) return CompleteProfileFirst();
            var result = await Service<KycService>().SubmitAsync(Me.CustomerId!.Value, Me.Account.Id, new KycSubmission
            {
                DocumentType = Input.DocumentType,
                DocumentNumber = Input.DocumentNumber,
                FullNameOnDocument = Input.FullNameOnDocument,
                DateOfBirth = Input.DateOfBirth,
                DocumentExpiresOn = Input.DocumentExpiresOn,
                Front = Input.Front,
                Back = Input.Back,
                Selfie = Input.Selfie,
            });
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                Latest = await Service<KycService>().LatestAsync(Me.CustomerId!.Value);
                return Page();
            }
            TempData["Success"] = result.Message;
            return RedirectToPage();
        }

        /// <summary>The customer's own document (to check what they sent).</summary>
        public async Task<IActionResult> OnGetDocumentAsync(Guid id)
        {
            if (await LoadMeAsync() is { } challenge) return challenge;
            var file = await Service<KycService>().OpenDocumentAsync(await HttpContext.AccessAsync(), id, Me.CustomerId);
            if (file is null) return NotFound();
            Response.Headers.CacheControl = "no-store";
            return PhysicalFile(file.Value.Path, file.Value.ContentType);
        }
    }
}
