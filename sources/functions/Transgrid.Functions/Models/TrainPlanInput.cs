using System.Text.Json.Serialization;

namespace Transgrid.Functions.Models;

/// <summary>
/// Represents a train plan received from the Operations API GraphQL endpoint.
/// </summary>
public record TrainPlanInput
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("serviceCode")]
    public string ServiceCode { get; init; } = string.Empty;

    [JsonPropertyName("pathway")]
    public string Pathway { get; init; } = string.Empty;

    [JsonPropertyName("travelDate")]
    public string TravelDate { get; init; } = string.Empty;

    [JsonPropertyName("passagePoints")]
    public List<PassagePoint> PassagePoints { get; init; } = new();

    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    [JsonPropertyName("destination")]
    public string Destination { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("planType")]
    public string PlanType { get; init; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; init; } = string.Empty;
}

/// <summary>
/// Represents a passage point in the train route.
/// </summary>
public record PassagePoint
{
    [JsonPropertyName("locationCode")]
    public string LocationCode { get; init; } = string.Empty;

    [JsonPropertyName("arrivalTime")]
    public string? ArrivalTime { get; init; }

    [JsonPropertyName("departureTime")]
    public string? DepartureTime { get; init; }
}
