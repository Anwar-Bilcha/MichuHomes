using RadiantHomes.Models.Shared; // Assuming BaseEntity is here
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using RadiantHomes.Models.HousingCompanies; // Make sure this is included for List<T>

namespace RadiantHomes.Models
{
    // PartyAddress represents a physical address that various parties (like companies, customers, projects) can be associated with.
    public class PartyAddress : BaseEntity // Inherits CreatedOn, CreatedBy, UpdatedOn, UpdatedBy
    {
        [Key] // Denotes AddressID as the primary key
        [Display(Name = "Address")]
        public Guid AddressID { get; set; }

        // --- Address Components (with sensible required fields) ---

        [Required(ErrorMessage = "City is required.")]
        [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
        public string City { get; set; } = string.Empty; // Initialize non-nullable string

        public string? Woreda { get; set; } // Nullable if optional
        [Display(Name = "Sub-city")]
        public string? SubCity { get; set; } // Nullable if optional

        public string? Region { get; set; } // Nullable if optional

        [StringLength(20, ErrorMessage = "Postal Code cannot exceed 20 characters.")]
        [Display(Name = "Postal code")]
        public string? PostalCode { get; set; } // Nullable if optional

        [Required(ErrorMessage = "Country is required.")]
        [StringLength(100, ErrorMessage = "Country cannot exceed 100 characters.")]
        public string Country { get; set; } = string.Empty; // Initialize non-nullable string

        [Required(ErrorMessage = "Street is required.")]
        [StringLength(200, ErrorMessage = "Street cannot exceed 200 characters.")]
        public string Street { get; set; } = string.Empty; // Initialize non-nullable string

        [Display(Name = "Main location")]
        public string? LocationMain { get; set; } // General location description (e.g., "Near Central Park")
        [Display(Name = "Sub-location")]
        public string? LocationSub { get; set; }  // More specific location details (e.g., "2nd floor, Suite 300")

        // --- Navigation Properties (Collections) ---
        // Initialize lists to prevent NullReferenceExceptions when adding items to newly created objects.

        public List<House> Houses { get; set; } = new List<House>();
        public List<Building> Buildings { get; set; } = new List<Building>();
        public List<HouseProject> Projects { get; set; } = new List<HouseProject>();
        public List<ApartmentHouse> ApartmentHouses { get; set; } = new List<ApartmentHouse>();
        public List<HousingCompany> RealEstates { get; set; } = new List<HousingCompany>();
        public List<Customer> Customers { get; set; } = new List<Customer>();

        // --- Computed Property for Formatted Address ---
        // [NotMapped] indicates that this property should not be mapped to a database column.
        // It provides a convenient way to get a display-friendly address string.
        [NotMapped]
        [Display(Name = "Address")]
        public string FormattedAddressFull => this.GetFormattedAddress(singleLine: false);

        [NotMapped]
        [Display(Name = "Address")]
        public string FormattedAddressCompact => this.GetFormattedAddress(singleLine: true);
    }

    // --- Extension Methods for PartyAddress ---
    // Provides helpful methods to format the address in different ways.
    public static class AddressExtensions
    {
        /// <summary>
        /// Generates a formatted address string for a PartyAddress object.
        /// </summary>
        /// <param name="address">The PartyAddress instance.</param>
        /// <param name="singleLine">If true, returns a comma-separated single line. If false, returns a multi-line string.</param>
        /// <returns>A formatted address string.</returns>
        public static string GetFormattedAddress(this PartyAddress address, bool singleLine = false)
        {
            var parts = new List<string>();

            // Add parts only if they are not null or empty, in a logical order.
            // Adjust the order and labels as per your preferred display format.
            if (!string.IsNullOrEmpty(address.Street)) parts.Add(address.Street);
            if (!string.IsNullOrEmpty(address.Woreda)) parts.Add($"Woreda: {address.Woreda}");
            if (!string.IsNullOrEmpty(address.SubCity)) parts.Add($"Sub City: {address.SubCity}");
            if (!string.IsNullOrEmpty(address.City)) parts.Add($"City: {address.City}");
            if (!string.IsNullOrEmpty(address.PostalCode)) parts.Add($"P.O. Box: {address.PostalCode}"); // Often includes PO box
            if (!string.IsNullOrEmpty(address.Region)) parts.Add($"Region: {address.Region}");
            if (!string.IsNullOrEmpty(address.Country)) parts.Add($"Country: {address.Country}");
            if (!string.IsNullOrEmpty(address.LocationMain)) parts.Add($"Location: {address.LocationMain}");
            if (!string.IsNullOrEmpty(address.LocationSub)) parts.Add($"Details: {address.LocationSub}");

            if (singleLine)
            {
                return string.Join(", ", parts); // e.g., "123 Main St, City: Addis Ababa, Country: Ethiopia"
            }
            else
            {
                // Multi-line output for detailed display
                return string.Join(System.Environment.NewLine, parts);
                /* Example output:
                 * 123 Main St
                 * Woreda: Bole
                 * Sub City: Bole Sub City
                 * City: Addis Ababa
                 * P.O. Box: 1000
                 * Region: Oromia
                 * Country: Ethiopia
                 * Location: Near xyz landmark
                 * Details: Building A, Apt 405
                 */
            }
        }
    }
}