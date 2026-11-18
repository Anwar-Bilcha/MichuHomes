using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.Kyc;
using RadiantHomes.Services.CustomerPortal;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Services.Kyc
{
    public enum KycAction { StartReview, Approve, Reject, RequestResubmission, Revoke }

    public sealed class KycSubmission
    {
        public string DocumentType { get; set; } = string.Empty;
        public string DocumentNumber { get; set; } = string.Empty;
        public string FullNameOnDocument { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public DateTime? DocumentExpiresOn { get; set; }
        public IFormFile? Front { get; set; }
        public IFormFile? Back { get; set; }
        public IFormFile? Selfie { get; set; }
    }

    /// <summary>
    /// Customer identity verification. Customers submit (portal), platform reviewers decide (Admin/Kyc). Only an
    /// approved verification unlocks verified-only features (video calls with agents; chat later). Documents never
    /// live under wwwroot and every opening by staff is audited.
    /// </summary>
    public sealed partial class KycService
    {
        public const long MaxFileBytes = 10 * 1024 * 1024;
        private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".pdf"] = "application/pdf",
        };

        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly AuditService _audit;
        private readonly CustomerNotifications _customers;

        public KycService(ApplicationDbContext db, IWebHostEnvironment env, AuditService audit, CustomerNotifications customers)
        {
            _db = db;
            _env = env;
            _audit = audit;
            _customers = customers;
        }

        public string Folder(Guid applicationId) => Path.Combine(_env.ContentRootPath, "App_Data", "kyc", applicationId.ToString("N"));

        public Task<KycApplication?> LatestAsync(Guid customerId) =>
            _db.KycApplications.AsNoTracking().Include(a => a.Documents).Include(a => a.History)
                .Where(a => a.CustomerId == customerId).OrderByDescending(a => a.SubmittedOn).FirstOrDefaultAsync();

        /// <summary>Server-side eligibility: the customer's identity verification is approved (and not revoked).</summary>
        public async Task<bool> IsVerifiedAsync(Guid? customerId) =>
            customerId is Guid id && await _db.UserProfiles.AnyAsync(p => p.Id == id && p.KycStatus == KycStatuses.Approved);

        // ---------- Customer ----------

        public async Task<AccessResult> SubmitAsync(Guid customerId, string userId, KycSubmission input)
        {
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.Id == customerId);
            if (profile is null) return AccessResult.Fail("Complete your profile first.");
            if (!KycStatuses.CanSubmit(profile.KycStatus))
                return AccessResult.Fail(profile.KycStatus == KycStatuses.Approved ? "Your identity is already verified." : "Your verification is already being processed.");

            var error = Validate(input);
            if (error is not null) return AccessResult.Fail(error);

            var application = new KycApplication
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                UserId = userId,
                Status = KycStatuses.Pending,
                DocumentType = input.DocumentType,
                DocumentNumber = input.DocumentNumber.Trim().ToUpperInvariant(),
                FullNameOnDocument = input.FullNameOnDocument.Trim(),
                DateOfBirth = input.DateOfBirth!.Value.Date,
                DocumentExpiresOn = input.DocumentExpiresOn?.Date,
                SubmittedOn = DateTime.UtcNow,
            };

            var folder = Folder(application.Id);
            Directory.CreateDirectory(folder);
            try
            {
                foreach (var (kind, file) in new[] { (KycDocumentKinds.Front, input.Front), (KycDocumentKinds.Back, input.Back), (KycDocumentKinds.Selfie, input.Selfie) })
                {
                    if (file is null || file.Length == 0) continue;
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var stored = Guid.NewGuid().ToString("N") + ext;
                    await using (var stream = new FileStream(Path.Combine(folder, stored), FileMode.CreateNew))
                    {
                        await file.CopyToAsync(stream);
                    }
                    application.Documents.Add(new KycDocument
                    {
                        Id = Guid.NewGuid(), ApplicationId = application.Id, Kind = kind, StoredFileName = stored,
                        ContentType = ContentTypes[ext], SizeBytes = file.Length, UploadedOn = DateTime.UtcNow,
                    });
                }

                var from = profile.KycStatus;
                profile.KycStatus = KycStatuses.Pending;
                _db.KycApplications.Add(application);
                _db.KycStatusChanges.Add(new KycStatusChange
                {
                    ApplicationId = application.Id, FromStatus = from, ToStatus = KycStatuses.Pending,
                    Reason = from == KycStatuses.NotSubmitted ? "Submitted." : "Submitted again.",
                    ChangedByUserId = userId, ChangedByName = $"{profile.FirstName} {profile.LastName}".Trim(), ChangedOn = DateTime.UtcNow,
                });
                _db.Notifications.Add(new Notification
                {
                    Title = "Identity verification to review",
                    Message = $"{profile.FirstName} {profile.LastName} submitted identity documents.",
                    RecipientRole = MichuHomesRoles.MichuAdmin,
                    Datatype = EntityAffecteds.UserProfiles.ToString(),
                    DataKey = application.Id.ToString(),
                    RedirectUrl = $"/Admin/Kyc/Review?id={application.Id}",
                    CreatedAt = DateTime.Now,
                });
                _customers.Add(customerId, "Verification submitted", "We received your identity documents. We'll let you know when they are reviewed.",
                    EntityAffecteds.UserProfiles, application.Id.ToString(), "/Portal/Kyc");
                _audit.Add(AuditCategories.Kyc, "KycSubmitted", $"{profile.FirstName} {profile.LastName} submitted identity verification ({KycDocumentTypes.Label(application.DocumentType)}).",
                    null, nameof(KycApplication), application.Id.ToString());
                await _db.SaveChangesAsync();
            }
            catch
            {
                try { Directory.Delete(folder, recursive: true); } catch { /* best effort */ }
                throw;
            }
            return AccessResult.Ok("Thank you. Your documents were sent for review.", application.Id.ToString());
        }

        private static string? Validate(KycSubmission input)
        {
            if (!KycDocumentTypes.All.Any(t => t.Value == input.DocumentType)) return "Choose the type of identity document.";
            var number = (input.DocumentNumber ?? string.Empty).Trim();
            if (number.Length is < 3 or > 60 || !DocumentNumberPattern().IsMatch(number)) return "Enter the document number as printed (letters, digits, spaces, - or /).";
            var name = (input.FullNameOnDocument ?? string.Empty).Trim();
            if (name.Length is < 2 or > 150) return "Enter your full name exactly as on the document.";
            if (input.DateOfBirth is not DateTime dob) return "Enter your date of birth.";
            var today = DateTime.UtcNow.Date;
            if (dob.Date > today.AddYears(-18)) return "You must be at least 18 years old.";
            if (dob.Date < today.AddYears(-120)) return "Check your date of birth.";
            if (input.DocumentExpiresOn is DateTime exp && exp.Date <= today) return "This document has expired. Please use a valid document.";
            if (input.Front is null || input.Front.Length == 0) return "Upload a photo or scan of the front of your document.";
            if (input.Selfie is null || input.Selfie.Length == 0) return "Upload a photo of yourself holding the document.";
            foreach (var (label, file) in new[] { ("front of the document", input.Front), ("back of the document", input.Back), ("photo with the document", input.Selfie) })
            {
                if (file is null || file.Length == 0) continue;
                var fileError = CheckFile(file);
                if (fileError is not null) return $"The {label}: {fileError}";
            }
            return null;
        }

        /// <summary>JPG, PNG or PDF up to 10 MB whose content really is that format (checked by its first bytes).</summary>
        public static string? CheckFile(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!ContentTypes.ContainsKey(ext)) return "use a JPG, PNG or PDF file.";
            if (file.Length > MaxFileBytes) return "the file is larger than 10 MB.";
            Span<byte> head = stackalloc byte[8];
            using var stream = file.OpenReadStream();
            var read = stream.Read(head);
            var ok = ext switch
            {
                ".png" => read >= 8 && head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                ".pdf" => read >= 4 && head[..4].SequenceEqual("%PDF"u8),
                _ => read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
            };
            return ok ? null : "the file content doesn't match its type.";
        }

        [GeneratedRegex(@"^[A-Za-z0-9 \-/]+$")]
        private static partial Regex DocumentNumberPattern();

        // ---------- Reviewers ----------

        public static (string Permission, string[] From, string To, bool ReasonRequired) Rule(KycAction action) => action switch
        {
            KycAction.StartReview => (Perm.KycReview, new[] { KycStatuses.Pending }, KycStatuses.UnderReview, false),
            KycAction.Approve => (Perm.KycReview, new[] { KycStatuses.Pending, KycStatuses.UnderReview }, KycStatuses.Approved, false),
            KycAction.Reject => (Perm.KycReview, new[] { KycStatuses.Pending, KycStatuses.UnderReview }, KycStatuses.Rejected, true),
            KycAction.RequestResubmission => (Perm.KycReview, new[] { KycStatuses.Pending, KycStatuses.UnderReview }, KycStatuses.ResubmissionRequired, true),
            KycAction.Revoke => (Perm.KycRevoke, new[] { KycStatuses.Approved }, KycStatuses.Revoked, true),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

        public static IEnumerable<KycAction> Available(UserAccess actor, string status) =>
            Enum.GetValues<KycAction>().Where(a => actor.IsPlatform && actor.Has(Rule(a).Permission) && Rule(a).From.Contains(status));

        public async Task<AccessResult> DecideAsync(UserAccess actor, Guid applicationId, KycAction action, string? reason, string? actorName)
        {
            var rule = Rule(action);
            if (!(actor.IsPlatform && actor.Has(rule.Permission))) return AccessResult.Fail("You don't have permission to do that.");
            var application = await _db.KycApplications.Include(a => a.Customer).FirstOrDefaultAsync(a => a.Id == applicationId);
            if (application is null) return AccessResult.Fail("That application doesn't exist.");
            if (await _db.KycApplications.AnyAsync(a => a.CustomerId == application.CustomerId && a.SubmittedOn > application.SubmittedOn))
                return AccessResult.Fail("The customer has submitted a newer application; review that one.");
            if (!rule.From.Contains(application.Status)) return AccessResult.Fail($"The application is \"{KycStatuses.Label(application.Status)}\"; that isn't possible now.");
            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (rule.ReasonRequired && reason is null) return AccessResult.Fail("Please give a reason. The customer will see it.");
            if (reason is { Length: > 1000 }) return AccessResult.Fail("Keep the reason under 1000 characters.");

            var from = application.Status;
            var customer = application.Customer;
            application.Status = rule.To;
            application.ReviewedOn = DateTime.UtcNow;
            application.ReviewedByUserId = actor.UserId;
            application.ReviewedByName = actorName;
            if (reason is not null) application.DecisionReason = reason;
            customer.KycStatus = rule.To;
            if (rule.To == KycStatuses.Approved) customer.ApprovedDate = DateTime.Now;
            _db.KycStatusChanges.Add(new KycStatusChange
            {
                ApplicationId = application.Id, FromStatus = from, ToStatus = rule.To, Reason = reason,
                ChangedByUserId = actor.UserId, ChangedByName = actorName, ChangedOn = DateTime.UtcNow,
            });
            var (title, message) = action switch
            {
                KycAction.StartReview => ((string?)null, (string?)null),
                KycAction.Approve => ("Identity verified", "Your identity is verified. You can now video call agents."),
                KycAction.Reject => ("Verification not approved", $"Your identity verification wasn't approved: {reason} You can submit new documents."),
                KycAction.RequestResubmission => ("Please resubmit your documents", $"We need new documents to verify your identity: {reason}"),
                _ => ("Verification revoked", $"Your identity verification was withdrawn: {reason}"),
            };
            if (title is not null) _customers.Add(customer.Id, title, message!, EntityAffecteds.UserProfiles, application.Id.ToString(), "/Portal/Kyc");
            _audit.Add(AuditCategories.Kyc, "Kyc" + action, $"{customer.FirstName} {customer.LastName}: {KycStatuses.Label(from)} → {KycStatuses.Label(rule.To)}{(reason is null ? "" : $" ({reason})")}.",
                null, nameof(KycApplication), application.Id.ToString());
            await _db.SaveChangesAsync();
            return AccessResult.Ok(action switch
            {
                KycAction.StartReview => "Review started.",
                KycAction.Approve => $"{customer.FirstName} {customer.LastName} is verified.",
                KycAction.Reject => "The application was rejected.",
                KycAction.RequestResubmission => "The customer was asked to resubmit.",
                _ => "The verification was revoked.",
            });
        }

        /// <summary>
        /// A document's file for a reviewer (kyc.view; the opening is audited) or for its owner. Null when not allowed
        /// or missing.
        /// </summary>
        public async Task<(string Path, string ContentType, string Name)?> OpenDocumentAsync(UserAccess actor, Guid documentId, Guid? ownCustomerId)
        {
            var document = await _db.KycDocuments.Include(d => d.Application).ThenInclude(a => a.Customer).FirstOrDefaultAsync(d => d.Id == documentId);
            if (document is null) return null;
            var isReviewer = actor.IsPlatform && actor.Has(Perm.KycView);
            var isOwner = ownCustomerId is Guid own && own == document.Application.CustomerId;
            if (!isReviewer && !isOwner) return null;

            var folder = Path.GetFullPath(Folder(document.ApplicationId));
            var path = Path.GetFullPath(Path.Combine(folder, document.StoredFileName));
            if (!path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return null;

            if (isReviewer && !isOwner)
            {
                var c = document.Application.Customer;
                _audit.Add(AuditCategories.Kyc, "KycDocumentViewed", $"Opened {KycDocumentKinds.Label(document.Kind).ToLowerInvariant()} of {c.FirstName} {c.LastName}.",
                    null, nameof(KycApplication), document.ApplicationId.ToString());
                await _db.SaveChangesAsync();
            }
            return (path, document.ContentType, $"{document.Kind.ToLowerInvariant()}{Path.GetExtension(document.StoredFileName)}");
        }
    }
}
