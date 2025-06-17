using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Models.HousingCompanies;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;

namespace RadiantHomes.Areas.HousingCompanyAdmin.Pages
{
    public class CreateModel : PageModel
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CreateModel> _logger;

        public SelectList AddressList { get; set; }

        public PartyAddress NewAddress { get; set; }

        [BindProperty]
        public HousingCompany HousingCompany { get; set; } = default!;

        public IFormFile LogoUrl { get; set; }

        public CreateModel(ApplicationDbContext context, IWebHostEnvironment env, ILogger<CreateModel> logger)
        {
            _env = env;
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var addresses = await _context.Addresses
                    .Select(a => new
                    {
                        a.AddressID,
                        FormattedAddress = $"{a.City}, {a.SubCity}- Wereda - {a.Woreda}"
                    })
                    .ToListAsync();

            AddressList = new SelectList(addresses, "AddressID", "FormattedAddress");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Validate the model first
            //if (!ModelState.IsValid)
            //{
            //    return Page(); // Return to the same page with validation errors
            //}
            AddressList = new SelectList(_context.Addresses.ToList(), "AddressID", "FormattedAddress");

            try
            {
                
                // Handle the logo upload
                if (LogoUrl != null)
                {
                    // Sanitize company name for use in directory name
                    var sanitizedCompanyName = Regex.Replace(HousingCompany.Name, @"[<>:""/\\|?*]", "");

                    // Directory path to store logo
                    var logodirectory = Path.Combine(_env.WebRootPath, "uploads", "CompanyLogos", sanitizedCompanyName);
                    if (!Directory.Exists(logodirectory))
                    {
                        Directory.CreateDirectory(logodirectory);
                    }

                    // Logo file path
                    var logofile = Path.Combine(logodirectory, LogoUrl.FileName);
                    using (var stream = new FileStream(logofile, FileMode.Create))
                    {
                        await LogoUrl.CopyToAsync(stream);
                    }

                    // Store relative URL of the logo in the HousingCompany model
                    HousingCompany.LogoUrl = Path.Combine("uploads", "CompanyLogos", sanitizedCompanyName, LogoUrl.FileName).Replace("\\", "/");
                }

                // Add HousingCompany to the context and save it
                _context.HousingCompanies.Add(HousingCompany);
                await _context.SaveChangesAsync();

                // Log company ID for debugging purposes
                _logger.LogInformation("Created a new Housing Company with ID = {CompanyId}", HousingCompany.CompanyId);

                // Redirect to the Financial page for the created HousingCompany
                return RedirectToPage("/HousingCompanyAdmin/projects/Create", new { companyId = HousingCompany.CompanyId });
            }
            catch (Exception ex)
            {
                // Log the error and display an error page
                _logger.LogError(ex, "An error occurred while creating the housing company.");
                return RedirectToPage("/Error");
            }
        }
    }
}
