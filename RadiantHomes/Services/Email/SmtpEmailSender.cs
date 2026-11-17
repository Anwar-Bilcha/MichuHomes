using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace RadiantHomes.Services.Email
{
    /// <summary>
    /// Email settings (section "Email"). Production: an SMTP server (host, port, user, password, from address), set in
    /// server-only settings or environment variables (Email__Host, Email__Password, ...). Development: PickupDirectory
    /// writes each message as an .eml file instead of sending it.
    /// </summary>
    public sealed class EmailSettings
    {
        public const string Section = "Email";
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        /// <summary>Use TLS (STARTTLS on 587, implicit on 465 is not supported by SmtpClient; use 587).</summary>
        public bool EnableSsl { get; set; } = true;
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? FromAddress { get; set; }
        public string FromName { get; set; } = "Michu Homes";
        /// <summary>Write messages as .eml files to this folder (relative to the app) instead of sending them.</summary>
        public string? PickupDirectory { get; set; }

        public bool CanSend => !string.IsNullOrWhiteSpace(PickupDirectory) || (!string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress));
    }

    /// <summary>
    /// Sends account emails (confirmation, password reset, staff invitations). Without settings nothing is sent and a
    /// warning is logged — never the message body, which can contain sign-in links.
    /// </summary>
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<SmtpEmailSender> _logger;
        private readonly string _contentRoot;

        public SmtpEmailSender(IOptions<EmailSettings> settings, ILogger<SmtpEmailSender> logger, IWebHostEnvironment env)
        {
            _settings = settings.Value;
            _logger = logger;
            _contentRoot = env.ContentRootPath;
        }

        public bool CanSend => _settings.CanSend;

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (!_settings.CanSend)
            {
                _logger.LogWarning("Email is not configured (Email:Host / Email:FromAddress): \"{Subject}\" to {Email} was not sent.", subject, email);
                return;
            }

            using var message = new MailMessage
            {
                From = new MailAddress(string.IsNullOrWhiteSpace(_settings.FromAddress) ? "no-reply@michuhomes.local" : _settings.FromAddress, _settings.FromName),
                Subject = subject,
                Body = Wrap(htmlMessage),
                IsBodyHtml = true,
            };
            message.To.Add(email);

            using var client = new SmtpClient();
            if (!string.IsNullOrWhiteSpace(_settings.PickupDirectory))
            {
                var folder = Path.IsPathRooted(_settings.PickupDirectory) ? _settings.PickupDirectory : Path.Combine(_contentRoot, _settings.PickupDirectory);
                Directory.CreateDirectory(folder);
                client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
                client.PickupDirectoryLocation = folder;
            }
            else
            {
                client.Host = _settings.Host!;
                client.Port = _settings.Port;
                client.EnableSsl = _settings.EnableSsl;
                if (!string.IsNullOrWhiteSpace(_settings.UserName))
                {
                    client.Credentials = new NetworkCredential(_settings.UserName, _settings.Password);
                }
            }

            try
            {
                await client.SendMailAsync(message);
                _logger.LogInformation("Email \"{Subject}\" sent to {Email}.", subject, email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sending email \"{Subject}\" to {Email} failed.", subject, email);
                throw;
            }
        }

        private static string Wrap(string body) =>
            "<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:15px;color:#1f2933;max-width:560px\">"
            + "<p style=\"font-size:18px;font-weight:600;color:#0f5f8c\">Michu Homes</p>"
            + body
            + "<p style=\"color:#6b7280;font-size:12px;margin-top:24px\">You received this email because of an account at Michu Homes. If you didn't expect it, you can ignore it.</p></div>";
    }
}
