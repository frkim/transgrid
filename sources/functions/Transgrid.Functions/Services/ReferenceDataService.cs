using Transgrid.Functions.Models;

namespace Transgrid.Functions.Services;

/// <summary>
/// Service for loading reference data (locations, vehicles) used in XML transformation.
/// </summary>
public interface IReferenceDataService
{
    /// <summary>
    /// Gets all reference data including locations and vehicles.
    /// </summary>
    Task<ReferenceData> GetReferenceDataAsync();

    /// <summary>
    /// Gets a specific location by code.
    /// </summary>
    LocationReference? GetLocation(string code);

    /// <summary>
    /// Gets the UIC code for a location code.
    /// </summary>
    string GetUicCode(string locationCode);
}

/// <summary>
/// Implementation of reference data service with cached data.
/// In production, this would load from Azure Table Storage or CosmosDB.
/// </summary>
public class ReferenceDataService : IReferenceDataService
{
    private readonly ReferenceData _referenceData;

    public ReferenceDataService()
    {
        // Initialize with Eurostar-specific reference data
        _referenceData = new ReferenceData
        {
            Locations = new Dictionary<string, LocationReference>
            {
                ["FRPNO"] = new() { Code = "FRPNO", Name = "Paris Nord", Country = "FR", UicCode = "8727100", Type = "STATION" },
                ["GBSTP"] = new() { Code = "GBSTP", Name = "London St Pancras", Country = "GB", UicCode = "7015400", Type = "STATION" },
                ["GBEBF"] = new() { Code = "GBEBF", Name = "Ebbsfleet International", Country = "GB", UicCode = "7015440", Type = "STATION" },
                ["GBASH"] = new() { Code = "GBASH", Name = "Ashford International", Country = "GB", UicCode = "7015480", Type = "STATION" },
                ["FRCQF"] = new() { Code = "FRCQF", Name = "Calais Fréthun", Country = "FR", UicCode = "8728600", Type = "STATION" },
                ["FRLIL"] = new() { Code = "FRLIL", Name = "Lille Europe", Country = "FR", UicCode = "8722326", Type = "STATION" },
                ["BEBMI"] = new() { Code = "BEBMI", Name = "Brussels Midi", Country = "BE", UicCode = "8814001", Type = "STATION" },
                ["NLAMA"] = new() { Code = "NLAMA", Name = "Amsterdam Centraal", Country = "NL", UicCode = "8400058", Type = "STATION" },
                ["FRDKQ"] = new() { Code = "FRDKQ", Name = "Dunkerque", Country = "FR", UicCode = "8727600", Type = "STATION" },
                ["FRCTL"] = new() { Code = "FRCTL", Name = "Channel Tunnel FR", Country = "FR", UicCode = "8799000", Type = "BORDER" },
                ["GBCTL"] = new() { Code = "GBCTL", Name = "Channel Tunnel UK", Country = "GB", UicCode = "7099000", Type = "BORDER" },
                ["FRMLY"] = new() { Code = "FRMLY", Name = "Marne-la-Vallée Chessy", Country = "FR", UicCode = "8711184", Type = "STATION" },
                ["FRCDG"] = new() { Code = "FRCDG", Name = "Paris CDG TGV", Country = "FR", UicCode = "8727141", Type = "STATION" }
            },
            Vehicles = new Dictionary<string, VehicleReference>
            {
                ["E300"] = new() { VehicleNumber = "E300", VehicleType = "EMU", TareWeight = 752, MaxPassengers = 750, TrainClass = "EUROSTAR" },
                ["E320"] = new() { VehicleNumber = "E320", VehicleType = "EMU", TareWeight = 965, MaxPassengers = 900, TrainClass = "EUROSTAR" }
            }
        };
    }

    public Task<ReferenceData> GetReferenceDataAsync()
    {
        return Task.FromResult(_referenceData);
    }

    public LocationReference? GetLocation(string code)
    {
        _referenceData.Locations.TryGetValue(code, out var location);
        return location;
    }

    public string GetUicCode(string locationCode)
    {
        if (_referenceData.Locations.TryGetValue(locationCode, out var location))
        {
            return location.UicCode;
        }
        // Return a default UIC format if not found
        return $"0000{locationCode}";
    }
}
