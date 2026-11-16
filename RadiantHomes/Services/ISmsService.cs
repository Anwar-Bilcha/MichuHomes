using Microsoft.Extensions.Options;
using RadiantHomes.Models.Shared;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace RadiantHomes.Services
{
    public interface ISmsService
    {
        public Task SendMessageAsync(string message, string sendTo);
    }

    public class SmsService : ISmsService
    {
        private readonly TwilioSettings _twilioSettings;

        public SmsService(IOptions<TwilioSettings> twilioOptions)
        {
            _twilioSettings = twilioOptions.Value;
        }

        public async Task SendMessageAsync(string Txtmessage, string sendToPhone)
        {
            if (!_twilioSettings.IsConfigured)
            {
                throw new InvalidOperationException(
                    "Twilio is not configured. Set Twilio:AccountSid, Twilio:AuthToken and Twilio:FromPhoneNumber " +
                    "(user-secrets locally, environment variables on the server).");
            }

            TwilioClient.Init(_twilioSettings.AccountSid, _twilioSettings.AuthToken);

            await MessageResource.CreateAsync(
                to: new PhoneNumber(sendToPhone),
                from: new PhoneNumber(_twilioSettings.FromPhoneNumber),
                body: Txtmessage
            );
        }
    }
}
