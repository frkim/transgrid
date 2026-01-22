namespace Transgrid.MockServer.Models;

/// <summary>
/// Request model for Salesforce extract simulation
/// </summary>
public class SalesforceExtractRequest
{
    /// <summary>
    /// Extract route: ALL, IDL_S3, GDS_AIR, or BENE
    /// </summary>
    public string ExtractRoute { get; set; } = "ALL";
    
    /// <summary>
    /// Priority filter: ALL, Priority, or Normal
    /// </summary>
    public string Priority { get; set; } = "ALL";
    
    /// <summary>
    /// Whether to simulate random failures
    /// </summary>
    public bool SimulateFailures { get; set; } = false;
}

/// <summary>
/// Request model for Salesforce Platform Event simulation
/// </summary>
public class PlatformEventRequest
{
    /// <summary>
    /// The event type to simulate
    /// </summary>
    public string? EventType { get; set; } = "NegotiatedRateExtract__e";
}

/// <summary>
/// Request model for Network Rail CIF processing simulation
/// </summary>
public class CifProcessRequest
{
    /// <summary>
    /// Processing type: HOURLY or WEEKLY
    /// </summary>
    public string ProcessingType { get; set; } = "HOURLY";
    
    /// <summary>
    /// Number of records to simulate
    /// </summary>
    public int RecordCount { get; set; } = 100;
    
    /// <summary>
    /// Rate of records to filter out (0.0 to 1.0)
    /// </summary>
    public double FilterRate { get; set; } = 0.3;
}

/// <summary>
/// Request model for calling the real Azure Function for CIF processing
/// </summary>
public class AzureCifProcessRequest
{
    /// <summary>
    /// File type: "update" for hourly delta, "full" for weekly full refresh
    /// </summary>
    public string? FileType { get; set; } = "update";
    
    /// <summary>
    /// Whether to force a full refresh bypassing caching
    /// </summary>
    public bool ForceRefresh { get; set; } = false;
}
