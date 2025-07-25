using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class CompanyFinancialDetail : BaseEntity
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string TaxId { get; set; }
        public decimal AnnualRevenue { get; set; }
        public bool IsPrimary { get; set; }
        public string TinNumber { get; set; }
        [Display(Name ="VAT Number")]
        public string VatRegistrationNumber { get; set; }
        [Display(Name = "Has Audited Statement")]
        public bool hasAuditedFinancialStatement { get; set; }
        [Display(Name = "Financial Document Url")]
        public string AuditedFinancialStatementUrl { get; set; }
        public int BudgetYear { get; set; }
        [ForeignKey(nameof(CompanyId))]
        public HousingCompany? Company { get; set; }
    }

}