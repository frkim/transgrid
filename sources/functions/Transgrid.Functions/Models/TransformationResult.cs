namespace Transgrid.Functions.Models;

/// <summary>
/// Result of XML transformation including validation status.
/// </summary>
public record TransformationResult
{
    public bool Success { get; init; }
    public string? Xml { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> ValidationErrors { get; init; } = new();
    public string ServiceCode { get; init; } = string.Empty;
    public string TravelDate { get; init; } = string.Empty;
    public DateTime TransformedAt { get; init; } = DateTime.UtcNow;
}
