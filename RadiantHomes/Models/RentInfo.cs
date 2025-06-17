namespace RadiantHomes.Models
{
    public class RentInfo
    {
        public Guid Id { get; set; }
        public Guid HouseId { get; set; }
        public House House { get; set; }
        public bool IsOnRent { get; set; }
        public RentTerms RentTerm { get; set; }
        public double PricePerTerm { get; set; }
        public double ExcepectedAdvancePayment { get; set; }
        public enum RentTerms
        {
            Monthly,
            Quarterly,
            Semi_Annually,
            Annually
        }
    }
}