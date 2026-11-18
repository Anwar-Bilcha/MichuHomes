using Microsoft.AspNetCore.Authorization;
using RadiantHomes.Services.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.DAL;
using RadiantHomes.Models;

namespace RadiantHomes.Pages.HouseOwner
{
    /// <summary>
    /// Serves a customer's uploaded ID document. ID files are blocked as static files
    /// (Program.cs), so this is the only way to open them: admins, or the profile's own user.
    /// </summary>
    [Authorize]
    public class IdDocumentModel : PageModel
    {
        private const string IdFolderUrl = "/uploads/ids/";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public IdDocumentModel(ApplicationDbContext context, UserManager<AppUser> userManager, IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        public async Task<IActionResult> OnGetAsync(Guid customerId)
        {
            var customer = await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer is null || string.IsNullOrWhiteSpace(customer.IdCardFileUrl)) return NotFound();

            // The owner, or staff with customers.documents.view (platform: any profile; company staff: their company's).
            var access = await HttpContext.AccessAsync();
            var user = await _userManager.GetUserAsync(User);
            var isOwnProfile = user?.CustomerId == customerId;
            if (!isOwnProfile && !access.Can(Perm.CustomerDocumentsView, customer.CompanyId)) return Forbid();

            // Only files under wwwroot/uploads/ids/ are served, and the resolved path must stay inside it.
            var url = customer.IdCardFileUrl.Replace('\\', '/');
            if (!url.StartsWith(IdFolderUrl, StringComparison.OrdinalIgnoreCase)) return NotFound();

            var idRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads", "ids"));
            var filePath = Path.GetFullPath(Path.Combine(idRoot, url[IdFolderUrl.Length..]));
            if (!filePath.StartsWith(idRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(filePath))
                return NotFound();

            if (!new FileExtensionContentTypeProvider().TryGetContentType(filePath, out var contentType))
                contentType = "application/octet-stream";

            Response.Headers["Cache-Control"] = "private, no-store";
            return PhysicalFile(filePath, contentType);
        }
    }
}
