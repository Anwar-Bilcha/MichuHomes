using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Utilities;

namespace RadiantHomes.Pages.Buildings
{
    /// <summary>
    /// Building type and construction status are stored as text (the enum member name, e.g. "Mixed_Use"), so the
    /// selects post member names and the pages map stored text back to the enum for display.
    /// </summary>
    public static class BuildingOptions
    {
        // Values written by the old building form that aren't enum members.
        private static readonly Dictionary<string, BuildingTypes> LegacyTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Mixed"] = BuildingTypes.Mixed_Use,
        };

        /// <summary>
        /// Options with display names (as Html.GetEnumSelectList&lt;T&gt;() shows them) and member-name values, because
        /// GetEnumSelectList posts the numeric value, which would end up in the text column as "2".
        /// </summary>
        public static IEnumerable<SelectListItem> Items<TEnum>() where TEnum : struct, Enum =>
            Enum.GetValues<TEnum>().Select(e => new SelectListItem(e.GetDisplayName(), e.ToString()));

        public static bool TryParseType(string? value, out BuildingTypes type)
        {
            if (TryParse(value, out type)) return true;
            return value != null && LegacyTypes.TryGetValue(value.Trim(), out type);
        }

        public static bool TryParseStatus(string? value, out ConstructionStatuses status) => TryParse(value, out status);

        /// <summary>Readable building type ("Mixed use"); unknown stored text is shown as is.</summary>
        public static string TypeName(string? value) => TryParseType(value, out var type) ? type.GetDisplayName() : value ?? string.Empty;

        /// <summary>Readable construction status ("Under finishing"); unknown stored text is shown as is.</summary>
        public static string StatusName(string? value) => TryParseStatus(value, out var status) ? status.GetDisplayName() : value ?? string.Empty;

        /// <summary>
        /// Normalises stored text to the member name for the edit form (so "Mixed" preselects "Mixed use").
        /// Text that matches nothing becomes empty, leaving the placeholder selected; the returned note says so.
        /// </summary>
        public static string? NormaliseForForm(Building building)
        {
            var unknown = new List<string>();
            if (TryParseType(building.BuildingType, out var type))
                building.BuildingType = type.ToString();
            else
            {
                if (!string.IsNullOrWhiteSpace(building.BuildingType)) unknown.Add($"building type \"{building.BuildingType}\"");
                building.BuildingType = string.Empty;
            }

            if (TryParseStatus(building.ConstructionStatus, out var status))
                building.ConstructionStatus = status.ToString();
            else
            {
                if (!string.IsNullOrWhiteSpace(building.ConstructionStatus)) unknown.Add($"construction status \"{building.ConstructionStatus}\"");
                building.ConstructionStatus = string.Empty;
            }

            return unknown.Count == 0
                ? null
                : $"The saved {string.Join(" and ", unknown)} isn't one of the options. Please choose one before saving.";
        }

        /// <summary>Server-side checks shared by Create and Edit.</summary>
        public static void Validate(ModelStateDictionary modelState, Building building)
        {
            const string p = nameof(Building) + ".";
            // Navigations and collections are never posted.
            foreach (var key in new[] { nameof(Building.Project), nameof(Building.Address), nameof(Building.AddressId),
                                        nameof(Building.Houses), nameof(Building.Photos), nameof(Building.TotalHousesPerFloor) })
            {
                modelState.Remove(p + key);
            }

            if (!Enum.GetNames<BuildingTypes>().Contains(building.BuildingType))
                modelState.AddModelError(p + nameof(Building.BuildingType), "Please select a building type.");
            if (!Enum.GetNames<ConstructionStatuses>().Contains(building.ConstructionStatus))
                modelState.AddModelError(p + nameof(Building.ConstructionStatus), "Please select a construction status.");

            var floorsKey = p + nameof(Building.NumberOfFloors);
            if ((!modelState.TryGetValue(floorsKey, out var floors) || floors.Errors.Count == 0)
                && (building.NumberOfFloors < 1 || building.NumberOfFloors > 200))
            {
                modelState.AddModelError(floorsKey, "Number of floors must be between 1 and 200.");
            }
        }

        // Accepts the member name ("Under_Finishing"), the display name ("Under finishing") or the name without
        // separators ("UnderFinishing"), ignoring case; never a number.
        private static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var wanted = Squash(value);
            foreach (var member in Enum.GetValues<TEnum>())
            {
                if (Squash(member.ToString()) == wanted || Squash(member.GetDisplayName()) == wanted)
                {
                    result = member;
                    return true;
                }
            }
            return false;
        }

        private static string Squash(string value) =>
            new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
