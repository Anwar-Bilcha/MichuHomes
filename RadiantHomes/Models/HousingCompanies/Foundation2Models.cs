using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models.HousingCompanies
{
    /// <summary>Every status change of a reservation, with who made it and why.</summary>
    public class ReservationStatusChange
    {
        public long Id { get; set; }
        public Guid TransactionId { get; set; }
        public ApartmentHouseSaleTransaction Transaction { get; set; } = null!;
        public HouseReserveStatuses? FromStatus { get; set; }
        public HouseReserveStatuses ToStatus { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
        [MaxLength(450)]
        public string? ChangedByUserId { get; set; }
        [MaxLength(256)]
        public string? ChangedByName { get; set; }
        public DateTime ChangedOn { get; set; }
    }

    public static class UploadBatchStatuses
    {
        public const string PendingReview = "PendingReview";
        public const string Approved = "Approved";
        public const string PartlyApproved = "PartlyApproved";
        public const string Rejected = "Rejected";

        public static string Label(string? s) => s switch
        {
            PendingReview => "Waiting for review",
            Approved => "Approved",
            PartlyApproved => "Partly approved",
            Rejected => "Rejected",
            _ => s ?? string.Empty,
        };

        public static string Badge(string? s) => s switch
        {
            Approved => "text-bg-success",
            PendingReview => "text-bg-warning",
            PartlyApproved => "text-bg-info",
            Rejected => "text-bg-danger",
            _ => "text-bg-light",
        };
    }

    public static class UploadRowStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    /// <summary>An Excel upload of apartments into one building, reviewed by the platform before anything is listed.</summary>
    public class UploadBatch
    {
        public Guid Id { get; set; }
        [MaxLength(20)]
        public string Reference { get; set; } = string.Empty;
        public Guid CompanyId { get; set; }
        public HousingCompany Company { get; set; } = null!;
        public Guid BuildingId { get; set; }
        public Building Building { get; set; } = null!;
        [MaxLength(260)]
        public string FileName { get; set; } = string.Empty;
        [MaxLength(30)]
        public string Status { get; set; } = UploadBatchStatuses.PendingReview;
        [MaxLength(450)]
        public string? UploadedByUserId { get; set; }
        [MaxLength(256)]
        public string? UploadedByName { get; set; }
        public DateTime UploadedOn { get; set; }
        [MaxLength(450)]
        public string? ReviewedByUserId { get; set; }
        [MaxLength(256)]
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedOn { get; set; }
        public List<UploadBatchRow> Rows { get; set; } = new();
    }

    public class UploadBatchRow
    {
        public Guid Id { get; set; }
        public Guid BatchId { get; set; }
        public UploadBatch Batch { get; set; } = null!;
        public int RowNumber { get; set; }
        public Guid ApartmentHouseId { get; set; }
        public ApartmentHouse ApartmentHouse { get; set; } = null!;
        [MaxLength(20)]
        public string Status { get; set; } = UploadRowStatuses.Pending;
        [MaxLength(1000)]
        public string? RejectReason { get; set; }
        public DateTime? DecidedOn { get; set; }
        [MaxLength(256)]
        public string? DecidedByName { get; set; }
        /// <summary>How many times the company sent this row back after a rejection.</summary>
        public int Resubmissions { get; set; }
    }
}

namespace RadiantHomes.Models.Shared
{
    /// <summary>Platform-wide settings editable by staff with settings.manage (Admin/Settings).</summary>
    public class PlatformSetting
    {
        [Key, MaxLength(100)]
        public string Key { get; set; } = string.Empty;
        [MaxLength(1000)]
        public string Value { get; set; } = string.Empty;
        [MaxLength(256)]
        public string? UpdatedBy { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
