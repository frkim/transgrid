namespace Transgrid.Functions.Models;

/// <summary>
/// Input model for negotiated rate data from Salesforce.
/// </summary>
public class NegotiatedRateInput
{
    public string Id { get; set; } = string.Empty;
    public string AccountManager { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string UniqueCode { get; set; } = string.Empty;
    public string CodeRecordType { get; set; } = string.Empty;
    public string? GdsUsed { get; set; }
    public string? Pcc { get; set; }
    public string? Distributor { get; set; }
    public string Road { get; set; } = string.Empty;
    public List<string> TariffCodes { get; set; } = new();
    public Dictionary<string, double> Discounts { get; set; } = new();
    public string Priority { get; set; } = "Normal";
    public string ActionType { get; set; } = "CREATE";
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}

/// <summary>
/// Request for generating CSV extracts from negotiated rates.
/// </summary>
public class NegotiatedRateExtractRequest
{
    public string ExtractRoute { get; set; } = "ALL";
    public string Priority { get; set; } = "ALL";
    public List<NegotiatedRateInput> NegotiatedRates { get; set; } = new();
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Result of CSV generation for a single route.
/// </summary>
public class RouteExtractResult
{
    public string RouteName { get; set; } = string.Empty;
    public string RouteCode { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int RecordCount { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string CsvContent { get; set; } = string.Empty;
}

/// <summary>
/// Response from the CSV generation function.
/// </summary>
public class NegotiatedRateExtractResponse
{
    public bool Success { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<RouteExtractResult> Routes { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
