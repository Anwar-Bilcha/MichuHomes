using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class CustomerTestimonial : BaseEntity
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public Customer Customer { get; set; }

        public Guid CompanyId { get; set; }
        public string Category { get; set; }
        public string CustomerName { get; set; }
        public string TestimonialText { get; set; }
        public DateTime SubmissionDate { get; set; }
        [ForeignKey(nameof(CompanyId))]
        public HousingCompany Company { get; set; }
        public int Rating { get; set; } // Optional: If you want to include a rating
    }
}
