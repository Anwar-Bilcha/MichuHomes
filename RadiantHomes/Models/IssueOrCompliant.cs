using Humanizer;
using Microsoft.CodeAnalysis;
using Microsoft.VisualBasic;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Model;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics.Metrics;
using System.Net.NetworkInformation;
using System.Net;
using System.Security.Policy;
using System.ComponentModel.DataAnnotations;

namespace RadiantHomes.Models
{
    public class IssueOrCompliant
    {
        [Display(Name = "Issue")]
        public Guid IssueId { get; set; }
        [Display(Name = "Issue")]
        public string IssueName { get; set; } = string.Empty;
        public House House { get; set; }
        [Display(Name = "House")]
        public Guid HouseId { get; set; }
        [Display(Name = "House owner")]
        public Guid HouseOwnerId { get; set; }
        [Display(Name = "House owner")]
        public Customer HouseOwner { get; set; }
        [Display(Name = "Issue category")]
        public string? IssueCategory { get; set; } // Incorrect Bed Rooms , inaccurate Price, Fake Photo , House Not for sale or Rent, Property Ownership is Mine, House has Legal issues, Other specify in text box below
        [Display(Name = "Description")]
        public string DetailedDescription { get; set; } 
        [Display(Name = "Severity")]
        public string? SeverityLevel { get; set; } //Low. Medium, High , Critical
        [Display(Name = "Supporting evidence")]
        public string? SupportingEvidenceUrl { get; set; }
        [Display(Name = "Additional evidence")]
        public string? SupportingEvidence2Url { get; set; }
        [Display(Name = "Complainant name")]
        public string? ComplainantFullName { get; set; }
        [Display(Name = "Preferred contact method")]
        public string? HowDoWeReachYou { get; set; } // Phone Call, Email, SMS, InPerson?
        [Display(Name = "Phone or email")]
        public string? PhoneOrEmail { get; set; }
        [Display(Name = "Complainant address")]
        public string? ComplainantAddress { get; set; }

    }
}
