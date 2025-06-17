using RadiantHomes.Models.DTOs;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RadiantHomes.Services
{
    public class ChapaPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public ChapaPaymentService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<string> InitiatePaymentAsync(ChapaRequestDTO payment, string callbackUrl, string returnUrl)
        {
            var chapaRequest = new ChapaRequestDTO
            {
                Amount = payment.Amount.ToString(),
                Currency = payment.Currency,
                Email = payment.Email,
                first_name = "Customer", // or fetch from user profile
                last_name = "User",      // optional
                tx_ref = payment.tx_ref,
                return_url = returnUrl,
                callback_url = callbackUrl,
                customization = new Customization
                {
                    title = "RadiantHomes Payment",
                    description = "Commission payment for platform service"
                }
            };

            var requestJson = JsonSerializer.Serialize(chapaRequest);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config["Chapa:SecretKey"]);

            var response = await _httpClient.PostAsync("https://api.chapa.co/v1/transaction/initialize", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception("Payment initiation failed: " + responseString);

            var chapaResponse = JsonSerializer.Deserialize<ChapaResponseDTO>(responseString);

            return chapaResponse?.data?.checkout_url ?? throw new Exception("Invalid Chapa response");
        }
    }

}
