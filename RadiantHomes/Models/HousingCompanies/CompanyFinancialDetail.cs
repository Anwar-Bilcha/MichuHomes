using RadiantHomes.Models.Shared;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiantHomes.Models.HousingCompanies
{
    public class CompanyFinancialDetail : BaseEntity
    {
        public Guid Id { get; set; }
        [Display(Name = "Company")]
        public Guid CompanyId { get; set; }
        [Display(Name = "Bank name")]
        public string BankName { get; set; }
        [Display(Name = "Account number")]
        public string AccountNumber { get; set; }
        [Display(Name = "Tax ID")]
        public string TaxId { get; set; }
        [Display(Name = "Annual revenue")]
        public decimal AnnualRevenue { get; set; }
        [Display(Name = "Primary account")]
        public bool IsPrimary { get; set; }
        [Display(Name = "TIN")]
        public string TinNumber { get; set; }
        [Display(Name = "VAT number")]
        public string VatRegistrationNumber { get; set; }
        [Display(Name = "Has audited statement")]
        public bool hasAuditedFinancialStatement { get; set; }
        [Display(Name = "Audited financial statement")]
        public string AuditedFinancialStatementUrl { get; set; }
        [Display(Name = "Budget year")]
        public int BudgetYear { get; set; }
        [ForeignKey(nameof(CompanyId))]
        public HousingCompany? Company { get; set; }
    }

}