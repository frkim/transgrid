namespace Transgrid.MockServer.Models;

public class NegotiatedRate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AccountManager { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string UniqueCode { get; set; } = string.Empty;
    public string CodeRecordType { get; set; } = string.Empty;
    public string GdsUsed { get; set; } = string.Empty;
    public string Pcc { get; set; } = string.Empty;
    public string Distributor { get; set; } = string.Empty;
    public string Road { get; set; } = string.Empty;
    public List<string> TariffCodes { get; set; } = new();
    public Dictionary<string, double> Discounts { get; set; } = new();
    public string Priority { get; set; } = "Normal";
    public string ActionType { get; set; } = "CREATE";
    public bool ExtractRequested { get; set; }
    public string B2bStatus { get; set; } = "Pending";
    public DateTime? B2bExtractDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
