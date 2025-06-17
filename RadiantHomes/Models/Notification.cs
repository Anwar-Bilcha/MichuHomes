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
        public Guid? ReceiverCompanyId { get; set; }
        public HousingCompany Company { get; set; }
        public Guid? ReceiverAdminId { get; set; }
        public Customer Admin { get; set; }
        public bool IsRead { get; set; } = false;
        public string? RedirectUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // To whom it is sent (Admin only for now, expandable)
        public string RecipientRole { get; set; } = MichuHomesRoles.MichuAdmin;
        public string DataKey { get; set; }
        [Required(ErrorMessage ="The Created item type is required")]
        [ValidEntityAffected]
        public string Datatype { get; set; }  

    }

}
