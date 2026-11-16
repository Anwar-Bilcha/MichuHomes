using RadiantHomes.DAL;
using RadiantHomes.Models.HousingCompanies;
using RadiantHomes.Utilities.Helpers;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        [Display(Name = "Company")]
        public Guid? ReceiverCompanyId { get; set; }
        public HousingCompany Company { get; set; }
        [Display(Name = "Admin")]
        public Guid? ReceiverAdminId { get; set; }
        public Customer Admin { get; set; }
        [Display(Name = "Read")]
        public bool IsRead { get; set; } = false;
        [Display(Name = "Link")]
        public string? RedirectUrl { get; set; }

        [Display(Name = "Created on")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // To whom it is sent (Admin only for now, expandable)
        [Display(Name = "Recipient role")]
        public string RecipientRole { get; set; } = MichuHomesRoles.MichuAdmin;
        [Display(Name = "Data key")]
        public string DataKey { get; set; }
        [Required(ErrorMessage ="The Created item type is required")]
        [ValidEntityAffected]
        [Display(Name = "Item type")]
        public string Datatype { get; set; }  

    }

}
