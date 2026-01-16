namespace Transgrid.MockServer.Models;

/// <summary>
/// Represents a GraphQL request for the mock API.
/// </summary>
public class GraphQLRequest
{
    /// <summary>
    /// The GraphQL query string.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Optional operation name for multi-operation documents.
    /// </summary>
    public string? OperationName { get; set; }

    /// <summary>
    /// Variables to be substituted in the query.
    /// </summary>
    public Dictionary<string, object>? Variables { get; set; }
}

/// <summary>
/// Represents an RNE export run result.
/// </summary>
public class RneExportRun
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime TravelDate { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public string ExportType { get; set; } = "DAILY"; // DAILY, D+2, RETRY
    public int TotalPlans { get; set; }
    public int SuccessfulExports { get; set; }
    public int FailedExports { get; set; }
    public List<RneExportResult> Results { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public string Status { get; set; } = "COMPLETED"; // IN_PROGRESS, COMPLETED, FAILED
}

/// <summary>
/// Represents an individual export result.
/// </summary>
public class RneExportResult
{
    public string TrainId { get; set; } = string.Empty;
    public string ServiceCode { get; set; } = string.Empty;
    public DateTime TravelDate { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? BlobPath { get; set; }
    public bool UploadedToPrimary { get; set; }
    public bool UploadedToBackup { get; set; }
    public string? XmlPreview { get; set; }
}

/// <summary>
/// Represents a failed export record for retry.
/// </summary>
public class FailedExportRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TrainId { get; set; } = string.Empty;
    public string ServiceCode { get; set; } = string.Empty;
    public DateTime TravelDate { get; set; }
    public string FailureReason { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRetryAt { get; set; }
}

/// <summary>
/// Request model for simulating an export.
/// </summary>
public class SimulateExportRequest
{
    public DateTime? TravelDate { get; set; }
    public string ExportType { get; set; } = "DAILY"; // DAILY, D+2, RETRY
    public bool SimulateFailures { get; set; } = false;
    public double FailureRate { get; set; } = 0.1; // 10% failure rate when simulating
}
