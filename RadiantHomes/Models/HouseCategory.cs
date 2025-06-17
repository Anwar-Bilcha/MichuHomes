namespace RadiantHomes.Models
{
    public class HouseCategory
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public List<House> Houses { get; set; }
    }
}
