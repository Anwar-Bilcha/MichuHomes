using Microsoft.Extensions.Options;
using RadiantHomes.Models.Shared;
using Twilio.Types;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

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
            TwilioClient.Init(TwilioSettings.AccountSID, TwilioSettings.AuthToken);

           
            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(sendToPhone),
                from: new PhoneNumber("+17753707633"),
                body: Txtmessage
            );
        }
    }
}
