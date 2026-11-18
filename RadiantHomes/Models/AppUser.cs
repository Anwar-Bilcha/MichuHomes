using Microsoft.AspNetCore.Identity;
using RadiantHomes.Models.HousingCompanies;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models
{
    public class AppUser : IdentityUser
    {
        public string? CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
        bool? IsLocked { get; set; }    
        bool IsEmployee { get; set; }
        public virtual Guid? CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }
        public Guid? HousingCompanyId { get; set; }
        [ForeignKey(nameof(HousingCompanyId))]
        public virtual HousingCompany? Company { get; set; }

        /// <summary>Customer, platform staff or company staff (Models/Security/AccessControl.cs). Company staff have a HousingCompanyId.</summary>
        public Security.AccountType AccountType { get; set; } = Security.AccountType.Customer;

        /// <summary>Staff member's name (customers keep theirs on the Customer profile).</summary>
        [System.ComponentModel.DataAnnotations.MaxLength(150)]
        public string? FullName { get; set; }
    }
   
}