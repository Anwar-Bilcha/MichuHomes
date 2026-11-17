using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using RadiantHomes.Models;

namespace RadiantHomes.Services.Email
{
    /// <summary>Account emails with sign-in links: password reset, staff invitation, email confirmation.</summary>
    public sealed class AccountEmails
    {
        private readonly UserManager<AppUser> _users;
        private readonly IEmailSender _sender;
        private readonly LinkGenerator _links;
        private readonly IHttpContextAccessor _http;
        private readonly EmailSettings _settings;

        public AccountEmails(UserManager<AppUser> users, IEmailSender sender, LinkGenerator links, IHttpContextAccessor http, IOptions<EmailSettings> settings)
        {
            _users = users;
            _sender = sender;
            _links = links;
            _http = http;
            _settings = settings.Value;
        }

        /// <summary>Whether emails can be delivered (an SMTP server or, in development, a pickup folder is set).</summary>
        public bool CanSend => _settings.CanSend;

        /// <summary>A link to choose a new password. <paramref name="invitation"/>: worded as a welcome for a new staff account.</summary>
        public async Task SendPasswordLinkAsync(AppUser user, bool invitation)
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var url = Link("/Account/ResetPassword", new { area = "Identity", code, email = user.Email });
            var name = HtmlEncoder.Default.Encode(user.FullName ?? user.Email ?? string.Empty);
            var link = $"<a href=\"{HtmlEncoder.Default.Encode(url)}\">";
            if (invitation)
            {
                await _sender.SendEmailAsync(user.Email!, "Your Michu Homes account",
                    $"<p>Hello {name},</p><p>An account was created for you at Michu Homes. {link}Choose your password</a> to sign in.</p><p>The link works for a limited time; ask your administrator for a new one if it has expired.</p>");
            }
            else
            {
                await _sender.SendEmailAsync(user.Email!, "Reset your Michu Homes password",
                    $"<p>Hello {name},</p><p>{link}Choose a new password</a> for your Michu Homes account.</p><p>If you didn't ask for this, you can ignore this email; your password stays the same.</p>");
            }
        }

        public async Task SendConfirmationAsync(AppUser user, string? returnUrl = null)
        {
            var token = await _users.GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var url = Link("/Account/ConfirmEmail", new { area = "Identity", userId = user.Id, code, returnUrl });
            await _sender.SendEmailAsync(user.Email!, "Confirm your email",
                $"<p>Please confirm your Michu Homes account by <a href=\"{HtmlEncoder.Default.Encode(url)}\">clicking here</a>.</p>");
        }

        private string Link(string page, object values)
        {
            var http = _http.HttpContext ?? throw new InvalidOperationException("Account emails need a request to build links.");
            return _links.GetUriByPage(http, page, handler: null, values) ?? throw new InvalidOperationException($"No route to {page}.");
        }
    }
}
