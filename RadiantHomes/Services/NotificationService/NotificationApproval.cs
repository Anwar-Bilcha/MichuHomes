using RadiantHomes.Models;

namespace RadiantHomes.Services.NotificationService
{
    /// <summary>Where the thing a notification asks about stands, read from that record (not from the notification's read flag).</summary>
    public enum NotificationApprovalState
    {
        /// <summary>The notification has nothing to approve, or the record it points to no longer exists.</summary>
        NotApplicable,
        Pending,
        PartlyApproved,
        Approved,
        Rejected,
        Cancelled,
        Completed,
        Closed,
    }

    /// <param name="State">Overall state.</param>
    /// <param name="Label">Short text for a badge, e.g. "Approved" or "3 of 9 approved".</param>
    public sealed record NotificationApproval(NotificationApprovalState State, string Label)
    {
        public static readonly NotificationApproval NotApplicable = new(NotificationApprovalState.NotApplicable, "—");

        public bool IsApplicable => State != NotificationApprovalState.NotApplicable;

        /// <summary>Bootstrap badge classes for the state (the notification pages use badges).</summary>
        public string BadgeClass => State switch
        {
            NotificationApprovalState.Approved or NotificationApprovalState.Completed => "badge bg-success",
            NotificationApprovalState.Pending or NotificationApprovalState.PartlyApproved => "badge bg-warning",
            NotificationApprovalState.Rejected or NotificationApprovalState.Cancelled => "badge bg-danger",
            _ => "badge bg-secondary",
        };
    }

    /// <summary>What kind of record a notification is about. The notification table has no explicit kind,
    /// so it is recognised from the fields the code that creates each notification fills in.</summary>
    public enum NotificationSubject
    {
        Other,
        /// <summary>"N houses pending approval" (Pages/ApartmentHouses/Create, Excel upload): DataKey lists the new houses' ids.</summary>
        HouseApproval,
        /// <summary>"X reserved a house" (Pages/ApartmentHouses/Details): DataKey is the house id; the request itself is an ApartmentHouseSaleTransaction.</summary>
        HouseReservation,
        /// <summary>"New User Profile Pending Approval" (Pages/HouseOwner/Create): DataKey is the UserProfiles (Customer) id.</summary>
        ProfileApproval,
    }

    public static class NotificationSubjects
    {
        private const string ReservationTitleSuffix = " reserved a house";
        private const string ReservationRedirectPrefix = "/ApartmentHouses/details?id=";

        public static NotificationSubject Of(Notification notification)
        {
            if (Is(notification.Datatype, EntityAffecteds.ApartmentHouses))
            {
                var isReservation =
                    (notification.Title ?? string.Empty).EndsWith(ReservationTitleSuffix, StringComparison.OrdinalIgnoreCase)
                    || (notification.RedirectUrl ?? string.Empty).StartsWith(ReservationRedirectPrefix, StringComparison.OrdinalIgnoreCase);
                if (isReservation)
                    return ParseIds(notification.DataKey).Count == 1 ? NotificationSubject.HouseReservation : NotificationSubject.Other;

                return ParseIds(notification.DataKey).Count > 0 ? NotificationSubject.HouseApproval : NotificationSubject.Other;
            }

            if (Is(notification.Datatype, EntityAffecteds.UserProfiles) && ParseIds(notification.DataKey).Count == 1)
                return NotificationSubject.ProfileApproval;

            return NotificationSubject.Other;
        }

        /// <summary>DataKey holds one id, or a comma-separated list of ids (with a trailing ", ").</summary>
        public static List<Guid> ParseIds(string? dataKey)
        {
            if (string.IsNullOrWhiteSpace(dataKey)) return new List<Guid>();
            return dataKey
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => Guid.TryParse(part, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
        }

        private static bool Is(string? datatype, EntityAffecteds type) =>
            string.Equals(datatype, type.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Approval rules for <c>ApartmentHouse.Status</c>. That one column holds three different things:
    /// the approval gate (the public listing shows only Status = "Approved"), the construction state that the
    /// Excel upload writes ("Fully Completed", "Completed", …) and the reservation state ("Reserved by Customer").
    /// Until approval gets its own column, approving can only be expressed by setting Status to "Approved".
    /// </summary>
    public static class ApartmentHouseApproval
    {
        public const string ApprovedStatus = "Approved";

        // Values the app writes (or the model documents) for a house that was approved and listed and has moved on.
        private static readonly string[] AfterApprovalStatuses = { "Reserved by Customer", "Sold", "Rented" };

        // A deliberate admin decision that approving a whole upload must not undo.
        private static readonly string[] KeepStatuses = { "Hidden" };

        // Values that only mean "not approved yet": replacing them loses nothing.
        private static readonly string[] PendingStatuses = { "Pending", "New" };

        /// <summary>The house has been approved (it is listed, or was listed and is now reserved/sold/rented).</summary>
        public static bool IsApproved(string? status) =>
            Matches(status, ApprovedStatus) || AfterApprovalStatuses.Any(s => Matches(status, s));

        /// <summary>Approving may change this house's status (it is not approved yet and not deliberately hidden).</summary>
        public static bool CanApprove(string? status) =>
            !IsApproved(status) && !KeepStatuses.Any(s => Matches(status, s));

        /// <summary>Approving replaces a value that carries information (e.g. a construction state), not just "pending".</summary>
        public static bool ApprovingLosesInformation(string? status) =>
            !string.IsNullOrWhiteSpace(status) && !PendingStatuses.Any(s => Matches(status, s));

        private static bool Matches(string? status, string value) =>
            string.Equals(status?.Trim(), value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Outcome of approving the houses of a "houses pending approval" notification.</summary>
    public sealed class HouseApprovalResult
    {
        public bool NotificationFound { get; init; } = true;
        /// <summary>False when the notification isn't a "houses pending approval" notification.</summary>
        public bool IsHouseApproval { get; init; } = true;
        public int HousesFound { get; set; }
        public int Approved { get; set; }
        public int AlreadyApproved { get; set; }
        /// <summary>Previous status values that approving replaced (value → number of houses), e.g. "Fully Completed" → 8.</summary>
        public Dictionary<string, int> Replaced { get; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Houses left unchanged because of their status (value → number of houses), e.g. "Hidden" → 1.</summary>
        public Dictionary<string, int> Skipped { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
