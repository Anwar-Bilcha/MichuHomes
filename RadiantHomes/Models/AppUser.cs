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
    }
   
}