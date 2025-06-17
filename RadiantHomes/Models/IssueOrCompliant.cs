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

namespace RadiantHomes.Models
{
    public class IssueOrCompliant
    {
        public Guid IssueId { get; set; }
        public string IssueName { get; set; } = string.Empty;
        public House House { get; set; }
        public Guid HouseId { get; set; }
        public Guid HouseOwnerId { get; set; }
        public Customer HouseOwner { get; set; }
        public string? IssueCategory { get; set; } // Incorrect Bed Rooms , inaccurate Price, Fake Photo , House Not for sale or Rent, Property Ownership is Mine, House has Legal issues, Other specify in text box below
        public string DetailedDescription { get; set; } 
        public string? SeverityLevel { get; set; } //Low. Medium, High , Critical
        public string? SupportingEvidenceUrl { get; set; }
        public string? SupportingEvidence2Url { get; set; }
        public string? ComplainantFullName { get; set; }
        public string? HowDoWeReachYou { get; set; } // Phone Call, Email, SMS, InPerson?
        public string? PhoneOrEmail { get; set; }
        public string? ComplainantAddress { get; set; }

    }
}
