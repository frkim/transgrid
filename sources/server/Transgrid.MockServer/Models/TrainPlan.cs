namespace Transgrid.MockServer.Models;

public class TrainPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ServiceCode { get; set; } = string.Empty;
    public string Pathway { get; set; } = string.Empty;
    public DateTime TravelDate { get; set; }
    public List<string> PassagePoints { get; set; } = new();
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public string PlanType { get; set; } = "STANDARD";
    public string Country { get; set; } = "GB";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
