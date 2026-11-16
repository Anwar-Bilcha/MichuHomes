using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models.Shared
{
    public class BaseEntity
    {
        [Display(Name = "Created by")]
        public string? CreatedBy { get; set; }
        [Display(Name = "Created on")]
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        [Display(Name = "Updated by")]
        public string?  UpdatedBy { get; set; }
        [Display(Name = "Updated on")]
        public DateTime? UpdatedOn { get; set; }
    }
}
