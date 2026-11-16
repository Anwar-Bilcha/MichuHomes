namespace RadiantHomes.Models.Shared
{
    /// <summary>
    /// Twilio SMS settings, bound from the "Twilio" configuration section.
    /// Values are NOT stored in source control: locally they come from .NET user-secrets
    /// (dotnet user-secrets set "Twilio:AuthToken" "…"), on servers from environment variables
    /// (Twilio__AccountSid, Twilio__AuthToken, Twilio__FromPhoneNumber).
    /// </summary>
    public class TwilioSettings
    {
        public const string SectionName = "Twilio";

        public string AccountSid { get; set; } = string.Empty;
        public string AuthToken { get; set; } = string.Empty;
        public string FromPhoneNumber { get; set; } = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AccountSid) &&
            !string.IsNullOrWhiteSpace(AuthToken) &&
            !string.IsNullOrWhiteSpace(FromPhoneNumber);
    }
}
