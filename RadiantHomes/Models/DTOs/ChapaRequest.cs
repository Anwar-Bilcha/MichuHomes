namespace RadiantHomes.Models.DTOs
{
    public class ChapaRequestDTO
    {
        public string Amount { get; set; }
        public string Currency { get; set; } = "ETB";
        public string Email { get; set; }
        public string first_name { get; set; }
        public string last_name { get; set; }
        public string tx_ref { get; set; }
        public string return_url { get; set; }
        public string callback_url { get; set; }
        public Customization customization { get; set; }
    }

    public class Customization
    {
        public string title { get; set; }
        public string description { get; set; }
    }

    public class ChapaResponseDTO
    {
        public JsonContent message { get; set; }
        public string status { get; set; }
        public ChapaResponseData data { get; set; } // Renamed to be more specific
    }

    public class ChapaResponseData
    {
        public string checkout_url { get; set; }
        // Add other successful response data properties if the API returns them
    }
}