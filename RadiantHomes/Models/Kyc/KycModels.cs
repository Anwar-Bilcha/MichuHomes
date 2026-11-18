using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models.Kyc
{
    // Customer identity verification (KYC). A customer submits an application with identity details and document
    // images; platform reviewers (permission kyc.review) decide. Documents are stored outside wwwroot
    // (App_Data/kyc/{applicationId}) and served only to reviewers (kyc.view) or their owner. The customer's current
    // status is kept on Customer.KycStatus; every change is in KycStatusChanges and the audit log.

    public static class KycStatuses
    {
        public const string NotSubmitted = "NotSubmitted";
        public const string Pending = "Pending";
        public const string UnderReview = "UnderReview";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string ResubmissionRequired = "ResubmissionRequired";
        public const string Revoked = "Revoked";

        public static readonly string[] Queue = { Pending, UnderReview, ResubmissionRequired, Approved, Rejected, Revoked };

        /// <summary>The customer may (re)submit an application.</summary>
        public static bool CanSubmit(string? status) => status is null or NotSubmitted or Rejected or ResubmissionRequired;

        public static string Label(string? status) => status switch
        {
            Pending => "Submitted, waiting for review",
            UnderReview => "Under review",
            Approved => "Verified",
            Rejected => "Rejected",
            ResubmissionRequired => "Resubmission required",
            Revoked => "Verification revoked",
            _ => "Not submitted",
        };

        public static string Badge(string? status) => status switch
        {
            Approved => "text-bg-success",
            Pending or UnderReview => "text-bg-warning",
            ResubmissionRequired => "text-bg-info",
            Rejected or Revoked => "text-bg-danger",
            _ => "text-bg-light",
        };
    }

    public static class KycDocumentKinds
    {
        public const string Front = "Front";
        public const string Back = "Back";
        public const string Selfie = "Selfie";

        public static string Label(string kind) => kind switch
        {
            Front => "Document, front",
            Back => "Document, back",
            Selfie => "Photo holding the document",
            _ => kind,
        };
    }

    public static class KycDocumentTypes
    {
        public static readonly (string Value, string Text)[] All =
        {
            ("NationalId", "National ID (Fayda)"),
            ("Passport", "Passport"),
            ("KebeleId", "Kebele ID"),
            ("DrivingLicence", "Driving licence"),
            ("ResidencePermit", "Residence permit"),
        };

        public static string Label(string? value) => All.FirstOrDefault(t => t.Value == value).Text ?? value ?? string.Empty;
    }

    public class KycApplication
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;
        [MaxLength(30)]
        public string Status { get; set; } = KycStatuses.Pending;
        [MaxLength(40)]
        public string DocumentType { get; set; } = string.Empty;
        [MaxLength(60)]
        public string DocumentNumber { get; set; } = string.Empty;
        [MaxLength(150)]
        public string FullNameOnDocument { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public DateTime? DocumentExpiresOn { get; set; }
        public DateTime SubmittedOn { get; set; }
        public DateTime? ReviewedOn { get; set; }
        [MaxLength(450)]
        public string? ReviewedByUserId { get; set; }
        [MaxLength(256)]
        public string? ReviewedByName { get; set; }
        /// <summary>The latest reason given to the customer (rejection, resubmission or revocation).</summary>
        [MaxLength(1000)]
        public string? DecisionReason { get; set; }
        public List<KycDocument> Documents { get; set; } = new();
        public List<KycStatusChange> History { get; set; } = new();
    }

    public class KycDocument
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public KycApplication Application { get; set; } = null!;
        [MaxLength(20)]
        public string Kind { get; set; } = KycDocumentKinds.Front;
        /// <summary>Random file name under App_Data/kyc/{ApplicationId}; never a user-supplied name.</summary>
        [MaxLength(80)]
        public string StoredFileName { get; set; } = string.Empty;
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime UploadedOn { get; set; }
    }

    public class KycStatusChange
    {
        public long Id { get; set; }
        public Guid ApplicationId { get; set; }
        public KycApplication Application { get; set; } = null!;
        [MaxLength(30)]
        public string? FromStatus { get; set; }
        [MaxLength(30)]
        public string ToStatus { get; set; } = string.Empty;
        [MaxLength(1000)]
        public string? Reason { get; set; }
        [MaxLength(450)]
        public string? ChangedByUserId { get; set; }
        [MaxLength(256)]
        public string? ChangedByName { get; set; }
        public DateTime ChangedOn { get; set; }
    }
}
