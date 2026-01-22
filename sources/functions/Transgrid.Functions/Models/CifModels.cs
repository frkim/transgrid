using System.Text.Json.Serialization;

namespace Transgrid.Functions.Models;

/// <summary>
/// CIF file processing request
/// </summary>
public class CifProcessRequest
{
    /// <summary>
    /// Type of CIF file to process: "update" or "full"
    /// </summary>
    [JsonPropertyName("fileType")]
    public string FileType { get; set; } = "update";
    
    /// <summary>
    /// If true, ignore deduplication and reprocess all records
    /// </summary>
    [JsonPropertyName("forceRefresh")]
    public bool ForceRefresh { get; set; } = false;
    
    /// <summary>
    /// Optional date range filter for schedules
    /// </summary>
    [JsonPropertyName("dateRange")]
    public DateRangeFilter? DateRange { get; set; }
    
    /// <summary>
    /// Source URL override (for testing)
    /// </summary>
    [JsonPropertyName("sourceUrl")]
    public string? SourceUrl { get; set; }
}

public class DateRangeFilter
{
    [JsonPropertyName("start")]
    public string? Start { get; set; }
    
    [JsonPropertyName("end")]
    public string? End { get; set; }
}

/// <summary>
/// CIF processing result
/// </summary>
public class CifProcessResult
{
    [JsonPropertyName("processId")]
    public string ProcessId { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = "completed";
    
    [JsonPropertyName("statistics")]
    public CifProcessingStatistics Statistics { get; set; } = new();
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Processing statistics
/// </summary>
public class CifProcessingStatistics
{
    [JsonPropertyName("totalLines")]
    public int TotalLines { get; set; }
    
    [JsonPropertyName("schedulesProcessed")]
    public int SchedulesProcessed { get; set; }
    
    [JsonPropertyName("schedulesFiltered")]
    public int SchedulesFiltered { get; set; }
    
    [JsonPropertyName("eventsPublished")]
    public int EventsPublished { get; set; }
    
    [JsonPropertyName("duplicatesSkipped")]
    public int DuplicatesSkipped { get; set; }
    
    [JsonPropertyName("parseErrors")]
    public int ParseErrors { get; set; }
    
    [JsonPropertyName("processingTimeMs")]
    public long ProcessingTimeMs { get; set; }
}

/// <summary>
/// CIF record wrapper (NDJSON format)
/// </summary>
public class CifRecord
{
    [JsonPropertyName("JsonScheduleV1")]
    public JsonScheduleV1? JsonScheduleV1 { get; set; }
    
    [JsonPropertyName("JsonTimetableV1")]
    public JsonTimetableV1? JsonTimetableV1 { get; set; }
    
    [JsonPropertyName("JsonAssociationV1")]
    public JsonAssociationV1? JsonAssociationV1 { get; set; }
}

/// <summary>
/// Timetable header record
/// </summary>
public class JsonTimetableV1
{
    [JsonPropertyName("classification")]
    public string Classification { get; set; } = string.Empty;
    
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
    
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;
}

/// <summary>
/// Schedule record from CIF file
/// </summary>
public class JsonScheduleV1
{
    [JsonPropertyName("CIF_train_uid")]
    public string CIF_train_uid { get; set; } = string.Empty;
    
    [JsonPropertyName("CIF_stp_indicator")]
    public string CIF_stp_indicator { get; set; } = string.Empty;
    
    [JsonPropertyName("schedule_start_date")]
    public string schedule_start_date { get; set; } = string.Empty;
    
    [JsonPropertyName("schedule_end_date")]
    public string schedule_end_date { get; set; } = string.Empty;
    
    [JsonPropertyName("schedule_days_runs")]
    public string schedule_days_runs { get; set; } = string.Empty;
    
    [JsonPropertyName("train_status")]
    public string? train_status { get; set; }
    
    [JsonPropertyName("train_category")]
    public string? train_category { get; set; }
    
    [JsonPropertyName("atoc_code")]
    public string? atoc_code { get; set; }
    
    [JsonPropertyName("applicable_timetable")]
    public string? applicable_timetable { get; set; }
    
    [JsonPropertyName("schedule_location")]
    public List<ScheduleLocationRecord>? schedule_location { get; set; }
}

/// <summary>
/// Location record within a schedule
/// </summary>
public class ScheduleLocationRecord
{
    [JsonPropertyName("location_type")]
    public string? location_type { get; set; }
    
    [JsonPropertyName("record_identity")]
    public string? record_identity { get; set; }
    
    [JsonPropertyName("tiploc_code")]
    public string tiploc_code { get; set; } = string.Empty;
    
    [JsonPropertyName("departure")]
    public string? departure { get; set; }
    
    [JsonPropertyName("arrival")]
    public string? arrival { get; set; }
    
    [JsonPropertyName("public_departure")]
    public string? public_departure { get; set; }
    
    [JsonPropertyName("public_arrival")]
    public string? public_arrival { get; set; }
    
    [JsonPropertyName("platform")]
    public string? platform { get; set; }
    
    [JsonPropertyName("pass")]
    public string? pass { get; set; }
}

/// <summary>
/// Association record from CIF file
/// </summary>
public class JsonAssociationV1
{
    [JsonPropertyName("main_train_uid")]
    public string? main_train_uid { get; set; }
    
    [JsonPropertyName("assoc_train_uid")]
    public string? assoc_train_uid { get; set; }
    
    [JsonPropertyName("assoc_start_date")]
    public string? assoc_start_date { get; set; }
    
    [JsonPropertyName("category")]
    public string? category { get; set; }
    
    [JsonPropertyName("location")]
    public string? location { get; set; }
}

/// <summary>
/// Protobuf event structure for InfrastructurePathwayConfirmed
/// </summary>
public class InfrastructurePathwayConfirmedEvent
{
    [JsonPropertyName("trainServiceNumber")]
    public string TrainServiceNumber { get; set; } = string.Empty;
    
    [JsonPropertyName("travelDate")]
    public string TravelDate { get; set; } = string.Empty;
    
    [JsonPropertyName("origin")]
    public string Origin { get; set; } = string.Empty;
    
    [JsonPropertyName("destination")]
    public string Destination { get; set; } = string.Empty;
    
    [JsonPropertyName("passagePoints")]
    public List<PassagePointEvent> PassagePoints { get; set; } = new();
    
    [JsonPropertyName("metadata")]
    public EventMetadata Metadata { get; set; } = new();
}

public class PassagePointEvent
{
    [JsonPropertyName("locationCode")]
    public string LocationCode { get; set; } = string.Empty;
    
    [JsonPropertyName("locationName")]
    public string LocationName { get; set; } = string.Empty;
    
    [JsonPropertyName("arrivalTime")]
    public string? ArrivalTime { get; set; }
    
    [JsonPropertyName("departureTime")]
    public string? DepartureTime { get; set; }
    
    [JsonPropertyName("platform")]
    public string? Platform { get; set; }
}

public class EventMetadata
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = "planning.short_term";
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "InfrastructurePathwayConfirmed";
    
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = string.Empty;
    
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("O");
}

/// <summary>
/// Station mapping for reference data
/// </summary>
public class StationMapping
{
    [JsonPropertyName("tiplocCode")]
    public string TiplocCode { get; set; } = string.Empty;
    
    [JsonPropertyName("stationCode")]
    public string StationCode { get; set; } = string.Empty;
    
    [JsonPropertyName("stationName")]
    public string StationName { get; set; } = string.Empty;
    
    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }
    
    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }
    
    [JsonPropertyName("isEurostarConnection")]
    public bool IsEurostarConnection { get; set; }
}
