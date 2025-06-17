using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RadiantHomes.Services;
using RadiantHomes.Models;
using System.Threading.Tasks;
using RadiantHomes.DAL;
using Microsoft.EntityFrameworkCore;

namespace RadiantHomes.Pages.Houses.Actions
{
    public class GetOwnersContactModel : PageModel
    {
        private readonly IHouseServices _houseService;
        private readonly ApplicationDbContext _context;
        private readonly ISmsService _smsService;

        // Properties for data binding
        public House House { get; set; }
        public Customer Owner { get; set; }
        public string ContactRequestMessage { get; set; }

        // This is the ID of the house from the query string
   //     [BindProperty(SupportsGet = true)]

        public GetOwnersContactModel(IHouseServices houseService, ApplicationDbContext context, ISmsService smsService)
        {
            _houseService = houseService;
            _context = context;
            _smsService = smsService;
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Fetch the house details
            House =  _houseService.GetHouseById(id);
            House.HouseAddress = new PartyAddress();
            House.HouseAddress = _context.Addresses.FirstOrDefault(a => a.AddressID == House.AddressId) ?? new();
            if (House == null)
            {
                return NotFound();
            }

            // Fetch the owner's contact details
            Owner = await _context.UserProfiles.FindAsync(House.HouseOwnerId)??new();

            if (Owner == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            
            if ( id== null)
            {
                return NotFound();
            }

            // Fetch the house details
            House = _context.Houses.Include(a => a.HouseAddress).FirstOrDefault(a => a.Id == id) ?? new();
           // House = _houseService.GetHouseById(id);

            if (House == null)
            {
                return NotFound();
            }

            // Fetch the owner's contact details
            Owner = await _context.UserProfiles.FindAsync(House.HouseOwnerId)?? new();

            if (Owner == null)
            {
                return NotFound();
            }
            string ApplicantPhone = "+251969034410";
            string messageBody = $" House Title: {House.Title} , House City: {House.HouseAddress.City}" +
                $"House Owners Full Name: {Owner.FirstName} {Owner.LastName}" +
                $"House Owners Phone: {Owner.PhoneNumber}" +
                $"House Owner Email: {Owner.Email}";
            await _smsService.SendMessageAsync(messageBody, ApplicantPhone);
            // Here you could initiate an action, e.g., log the request, send an email, etc.
            ContactRequestMessage = $"House Owner's contact information for '{House.Title}' has been successfully sent to your Mobile Phone {ApplicantPhone} in Text message.";

            return Page();
        }
    }
}
