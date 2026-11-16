using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models.HousingCompanies
{
    public class CustomerTestimonial : BaseEntity
    {
        public Guid Id { get; set; }
        [Display(Name = "Customer")]
        public Guid CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public Customer Customer { get; set; }

        [Display(Name = "Company")]
        public Guid CompanyId { get; set; }
        public string Category { get; set; }
        [Display(Name = "Customer name")]
        public string CustomerName { get; set; }
        [Display(Name = "Testimonial")]
        public string TestimonialText { get; set; }
        [Display(Name = "Submission date")]
        public DateTime SubmissionDate { get; set; }
        [ForeignKey(nameof(CompanyId))]
        public HousingCompany Company { get; set; }
        public int Rating { get; set; } // Optional: If you want to include a rating
    }
}
