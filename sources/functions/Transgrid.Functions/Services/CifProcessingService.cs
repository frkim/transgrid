using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Transgrid.Functions.Models;

namespace Transgrid.Functions.Services;

/// <summary>
/// Service for processing CIF files from Network Rail
/// </summary>
public interface ICifProcessingService
{
    /// <summary>
    /// Process a CIF file stream and return statistics
    /// </summary>
    Task<CifProcessResult> ProcessCifStreamAsync(
        Stream stream, 
        string runId, 
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process raw NDJSON content
    /// </summary>
    Task<CifProcessResult> ProcessCifContentAsync(
        string content,
        string runId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Transform a schedule to event format
    /// </summary>
    InfrastructurePathwayConfirmedEvent TransformToEvent(JsonScheduleV1 schedule, string runId);
}

/// <summary>
/// Implementation of CIF processing service
/// </summary>
public class CifProcessingService : ICifProcessingService
{
    private readonly ILogger<CifProcessingService> _logger;
    private readonly IReferenceDataService _referenceDataService;
    private readonly HashSet<string> _processedKeys = new();
    private readonly JsonSerializerOptions _jsonOptions;
    
    // Station mapping cache (TIPLOC to CRS/Station info)
    private static readonly Dictionary<string, StationMapping> StationMappings = new()
    {
        // Major London Stations
        { "EUSTON", new StationMapping { TiplocCode = "EUSTON", StationCode = "EUS", StationName = "London Euston", IsEurostarConnection = false } },
        { "KNGX", new StationMapping { TiplocCode = "KNGX", StationCode = "KGX", StationName = "London King's Cross", IsEurostarConnection = false } },
        { "STPX", new StationMapping { TiplocCode = "STPX", StationCode = "STP", StationName = "London St Pancras", IsEurostarConnection = true } },
        { "STPANCI", new StationMapping { TiplocCode = "STPANCI", StationCode = "STP", StationName = "London St Pancras International", IsEurostarConnection = true } },
        { "PADTON", new StationMapping { TiplocCode = "PADTON", StationCode = "PAD", StationName = "London Paddington", IsEurostarConnection = false } },
        { "VICTRIA", new StationMapping { TiplocCode = "VICTRIA", StationCode = "VIC", StationName = "London Victoria", IsEurostarConnection = false } },
        { "WATRLMN", new StationMapping { TiplocCode = "WATRLMN", StationCode = "WAT", StationName = "London Waterloo", IsEurostarConnection = false } },
        { "LIVST", new StationMapping { TiplocCode = "LIVST", StationCode = "LST", StationName = "London Liverpool Street", IsEurostarConnection = false } },
        { "CHRX", new StationMapping { TiplocCode = "CHRX", StationCode = "CHX", StationName = "London Charing Cross", IsEurostarConnection = false } },
        
        // Major UK Cities
        { "BHAM", new StationMapping { TiplocCode = "BHAM", StationCode = "BHM", StationName = "Birmingham New Street", IsEurostarConnection = false } },
        { "BHAMNWS", new StationMapping { TiplocCode = "BHAMNWS", StationCode = "BHM", StationName = "Birmingham New Street", IsEurostarConnection = false } },
        { "MNCRPIC", new StationMapping { TiplocCode = "MNCRPIC", StationCode = "MAN", StationName = "Manchester Piccadilly", IsEurostarConnection = false } },
        { "LEEDS", new StationMapping { TiplocCode = "LEEDS", StationCode = "LDS", StationName = "Leeds", IsEurostarConnection = false } },
        { "EDINBUR", new StationMapping { TiplocCode = "EDINBUR", StationCode = "EDB", StationName = "Edinburgh Waverley", IsEurostarConnection = false } },
        { "GLGC", new StationMapping { TiplocCode = "GLGC", StationCode = "GLC", StationName = "Glasgow Central", IsEurostarConnection = false } },
        { "BRSTLTM", new StationMapping { TiplocCode = "BRSTLTM", StationCode = "BRI", StationName = "Bristol Temple Meads", IsEurostarConnection = false } },
        { "CRDFCNT", new StationMapping { TiplocCode = "CRDFCNT", StationCode = "CDF", StationName = "Cardiff Central", IsEurostarConnection = false } },
        { "YORK", new StationMapping { TiplocCode = "YORK", StationCode = "YRK", StationName = "York", IsEurostarConnection = false } },
        { "NEWCSTLE", new StationMapping { TiplocCode = "NEWCSTLE", StationCode = "NCL", StationName = "Newcastle", IsEurostarConnection = false } },
        
        // Eurostar Connections
        { "ASHFKY", new StationMapping { TiplocCode = "ASHFKY", StationCode = "AFK", StationName = "Ashford International", IsEurostarConnection = true } },
        { "EBSFDOM", new StationMapping { TiplocCode = "EBSFDOM", StationCode = "EBD", StationName = "Ebbsfleet International", IsEurostarConnection = true } },
        
        // Other Major Stations
        { "RDNGSTN", new StationMapping { TiplocCode = "RDNGSTN", StationCode = "RDG", StationName = "Reading", IsEurostarConnection = false } },
        { "OXFD", new StationMapping { TiplocCode = "OXFD", StationCode = "OXF", StationName = "Oxford", IsEurostarConnection = false } },
        { "CAMBDGE", new StationMapping { TiplocCode = "CAMBDGE", StationCode = "CBG", StationName = "Cambridge", IsEurostarConnection = false } },
        { "SOTON", new StationMapping { TiplocCode = "SOTON", StationCode = "SOU", StationName = "Southampton Central", IsEurostarConnection = false } },
        { "BRGHTNS", new StationMapping { TiplocCode = "BRGHTNS", StationCode = "BTN", StationName = "Brighton", IsEurostarConnection = false } },
        { "LIVRPL", new StationMapping { TiplocCode = "LIVRPL", StationCode = "LIV", StationName = "Liverpool Lime Street", IsEurostarConnection = false } },
        { "SHEFFLD", new StationMapping { TiplocCode = "SHEFFLD", StationCode = "SHF", StationName = "Sheffield", IsEurostarConnection = false } },
        { "NTTM", new StationMapping { TiplocCode = "NTTM", StationCode = "NOT", StationName = "Nottingham", IsEurostarConnection = false } },
        { "EXETSD", new StationMapping { TiplocCode = "EXETSD", StationCode = "EXD", StationName = "Exeter St Davids", IsEurostarConnection = false } },
        { "PLYMTH", new StationMapping { TiplocCode = "PLYMTH", StationCode = "PLY", StationName = "Plymouth", IsEurostarConnection = false } },
        { "MKTNKYL", new StationMapping { TiplocCode = "MKTNKYL", StationCode = "MKC", StationName = "Milton Keynes Central", IsEurostarConnection = false } },
    };

    public CifProcessingService(
        ILogger<CifProcessingService> logger,
        IReferenceDataService referenceDataService)
    {
        _logger = logger;
        _referenceDataService = referenceDataService;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };
    }

    public async Task<CifProcessResult> ProcessCifStreamAsync(
        Stream stream,
        string runId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new CifProcessResult
        {
            ProcessId = runId,
            Status = "processing"
        };
        var stats = result.Statistics;

        try
        {
            // Check if it's a GZIP stream by reading the magic bytes
            var isGzip = await IsGzipStreamAsync(stream);
            stream.Position = 0;

            Stream readStream = isGzip 
                ? new GZipStream(stream, CompressionMode.Decompress) 
                : stream;

            using var reader = new StreamReader(readStream, Encoding.UTF8, bufferSize: 8192);
            
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                await ProcessLineAsync(line, runId, forceRefresh, stats, result.Errors);
            }

            result.Status = "completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing CIF stream");
            result.Status = "failed";
            result.Errors.Add($"Stream processing error: {ex.Message}");
        }

        stats.ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        
        _logger.LogInformation(
            "CIF processing completed. RunId: {RunId}, TotalLines: {TotalLines}, Processed: {Processed}, Published: {Published}, Filtered: {Filtered}",
            runId, stats.TotalLines, stats.SchedulesProcessed, stats.EventsPublished, stats.SchedulesFiltered);

        return result;
    }

    public async Task<CifProcessResult> ProcessCifContentAsync(
        string content,
        string runId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new CifProcessResult
        {
            ProcessId = runId,
            Status = "processing"
        };
        var stats = result.Statistics;

        try
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                await ProcessLineAsync(line.Trim(), runId, forceRefresh, stats, result.Errors);
            }

            result.Status = "completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing CIF content");
            result.Status = "failed";
            result.Errors.Add($"Content processing error: {ex.Message}");
        }

        stats.ProcessingTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        
        return result;
    }

    private async Task ProcessLineAsync(
        string line,
        string runId,
        bool forceRefresh,
        CifProcessingStatistics stats,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        
        stats.TotalLines++;

        try
        {
            var record = JsonSerializer.Deserialize<CifRecord>(line, _jsonOptions);
            
            // Only process schedule records
            if (record?.JsonScheduleV1 == null) return;

            var schedule = record.JsonScheduleV1;

            // Filter: Only planning schedules (N = New)
            if (schedule.CIF_stp_indicator != "N")
            {
                stats.SchedulesFiltered++;
                return;
            }

            // Filter: Must have location data
            if (schedule.schedule_location == null || schedule.schedule_location.Count == 0)
            {
                stats.SchedulesFiltered++;
                return;
            }

            // Filter: Must have at least one mapped location
            var hasValidLocations = schedule.schedule_location.Any(loc => 
                StationMappings.ContainsKey(loc.tiploc_code));
            
            if (!hasValidLocations)
            {
                stats.SchedulesFiltered++;
                return;
            }

            stats.SchedulesProcessed++;

            // Deduplication check
            var dedupKey = $"{schedule.CIF_train_uid}_{schedule.schedule_start_date}";
            if (!forceRefresh && _processedKeys.Contains(dedupKey))
            {
                stats.DuplicatesSkipped++;
                return;
            }

            // Transform to event
            var eventData = TransformToEvent(schedule, runId);
            
            // Simulate publishing (in real implementation, this would call gRPC)
            await SimulatePublishAsync(eventData);
            
            _processedKeys.Add(dedupKey);
            stats.EventsPublished++;
        }
        catch (JsonException ex)
        {
            stats.ParseErrors++;
            _logger.LogDebug("Failed to parse CIF line: {Error}", ex.Message);
        }
    }

    public InfrastructurePathwayConfirmedEvent TransformToEvent(JsonScheduleV1 schedule, string runId)
    {
        var passagePoints = schedule.schedule_location?
            .Where(loc => StationMappings.ContainsKey(loc.tiploc_code))
            .Select(loc =>
            {
                var mapping = StationMappings[loc.tiploc_code];
                return new PassagePointEvent
                {
                    LocationCode = mapping.StationCode,
                    LocationName = mapping.StationName,
                    ArrivalTime = FormatTime(loc.arrival ?? loc.public_arrival),
                    DepartureTime = FormatTime(loc.departure ?? loc.public_departure),
                    Platform = loc.platform ?? ""
                };
            })
            .ToList() ?? new List<PassagePointEvent>();

        var origin = passagePoints.FirstOrDefault()?.LocationCode ?? "UNKNOWN";
        var destination = passagePoints.LastOrDefault()?.LocationCode ?? "UNKNOWN";

        return new InfrastructurePathwayConfirmedEvent
        {
            TrainServiceNumber = schedule.CIF_train_uid,
            TravelDate = schedule.schedule_start_date,
            Origin = origin,
            Destination = destination,
            PassagePoints = passagePoints,
            Metadata = new EventMetadata
            {
                Domain = "planning.short_term",
                Name = "InfrastructurePathwayConfirmed",
                CorrelationId = runId,
                Timestamp = DateTime.UtcNow.ToString("O")
            }
        };
    }

    private static string? FormatTime(string? time)
    {
        if (string.IsNullOrEmpty(time)) return null;
        
        // CIF times are in HHMM or HHMM½ format
        var cleanTime = time.Replace("½", "");
        if (cleanTime.Length >= 4)
        {
            return $"{cleanTime[..2]}:{cleanTime[2..4]}";
        }
        return time;
    }

    private static async Task<bool> IsGzipStreamAsync(Stream stream)
    {
        var buffer = new byte[2];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 2));
        return bytesRead >= 2 && buffer[0] == 0x1f && buffer[1] == 0x8b;
    }

    private Task SimulatePublishAsync(InfrastructurePathwayConfirmedEvent eventData)
    {
        // In a real implementation, this would publish via gRPC to the message store
        _logger.LogDebug(
            "Published event: {TrainId} on {Date} from {Origin} to {Destination}",
            eventData.TrainServiceNumber,
            eventData.TravelDate,
            eventData.Origin,
            eventData.Destination);
        
        return Task.CompletedTask;
    }
}
