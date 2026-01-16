namespace Transgrid.Functions.Models;

/// <summary>
/// Reference data for location codes, used for TAF-JSG XML generation.
/// </summary>
public record LocationReference
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string UicCode { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty; // STATION, BORDER, JUNCTION
}

/// <summary>
/// Reference data for train composition and vehicle information.
/// </summary>
public record VehicleReference
{
    public string VehicleNumber { get; init; } = string.Empty;
    public string VehicleType { get; init; } = string.Empty;
    public int TareWeight { get; init; }
    public int MaxPassengers { get; init; }
    public string TrainClass { get; init; } = string.Empty;
}

/// <summary>
/// Container for all reference data used in XML transformation.
/// </summary>
public record ReferenceData
{
    public Dictionary<string, LocationReference> Locations { get; init; } = new();
    public Dictionary<string, VehicleReference> Vehicles { get; init; } = new();
}
