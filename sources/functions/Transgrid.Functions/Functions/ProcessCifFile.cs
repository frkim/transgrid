using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Transgrid.Functions.Models;
using Transgrid.Functions.Services;

namespace Transgrid.Functions;

/// <summary>
/// Azure Functions for processing Network Rail CIF files (Use Case 3)
/// </summary>
public class ProcessCifFile
{
    private readonly ILogger<ProcessCifFile> _logger;
    private readonly ICifProcessingService _cifProcessingService;

    public ProcessCifFile(
        ILogger<ProcessCifFile> logger,
        ICifProcessingService cifProcessingService)
    {
        _logger = logger;
        _cifProcessingService = cifProcessingService;
    }

    /// <summary>
    /// Timer-triggered function for hourly CIF updates
    /// Runs every hour at :00
    /// </summary>
    [Function("ProcessCifUpdates")]
    public async Task ProcessCifUpdates(
        [TimerTrigger("0 0 * * * *")] TimerInfo timer)
    {
        var runId = Guid.NewGuid().ToString();
        _logger.LogInformation(
            "ProcessCifUpdates timer triggered. RunId: {RunId}, ScheduledTime: {ScheduledTime}",
            runId, timer.ScheduleStatus?.Last);

        try
        {
            // In production, this would download from Network Rail NTROD API
            // For demo, we generate sample data
            var sampleContent = GenerateSampleCifContent(50);
            
            var result = await _cifProcessingService.ProcessCifContentAsync(
                sampleContent, runId, forceRefresh: false);
            
            _logger.LogInformation(
                "ProcessCifUpdates completed. Processed: {Processed}, Published: {Published}",
                result.Statistics.SchedulesProcessed,
                result.Statistics.EventsPublished);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessCifUpdates failed. RunId: {RunId}", runId);
            throw;
        }
    }

    /// <summary>
    /// Timer-triggered function for weekly full CIF refresh
    /// Runs every Sunday at 02:00 UTC
    /// </summary>
    [Function("ProcessCifFullRefresh")]
    public async Task ProcessCifFullRefresh(
        [TimerTrigger("0 0 2 * * 0")] TimerInfo timer)
    {
        var runId = Guid.NewGuid().ToString();
        _logger.LogInformation(
            "ProcessCifFullRefresh timer triggered. RunId: {RunId}, ScheduledTime: {ScheduledTime}",
            runId, timer.ScheduleStatus?.Last);

        try
        {
            // In production, this would download full timetable from Network Rail
            // For demo, we generate sample data
            var sampleContent = GenerateSampleCifContent(200);
            
            var result = await _cifProcessingService.ProcessCifContentAsync(
                sampleContent, runId, forceRefresh: true);
            
            _logger.LogInformation(
                "ProcessCifFullRefresh completed. Processed: {Processed}, Published: {Published}",
                result.Statistics.SchedulesProcessed,
                result.Statistics.EventsPublished);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessCifFullRefresh failed. RunId: {RunId}", runId);
            throw;
        }
    }

    /// <summary>
    /// HTTP-triggered function for on-demand CIF processing
    /// Used for testing, reprocessing, and admin console integration
    /// </summary>
    [Function("ProcessCifOnDemand")]
    public async Task<HttpResponseData> ProcessCifOnDemand(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var runId = Guid.NewGuid().ToString();
        var correlationId = GetCorrelationId(req);
        
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RunId"] = runId
        });

        _logger.LogInformation("ProcessCifOnDemand function started. RunId: {RunId}", runId);

        try
        {
            // Parse request
            var requestBody = await req.ReadAsStringAsync();
            CifProcessRequest? request = null;
            
            if (!string.IsNullOrWhiteSpace(requestBody))
            {
                try
                {
                    request = JsonSerializer.Deserialize<CifProcessRequest>(requestBody, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse request body, using defaults");
                }
            }
            
            request ??= new CifProcessRequest();
            
            var recordCount = request.FileType?.ToLower() == "full" ? 200 : 50;
            var sampleContent = GenerateSampleCifContent(recordCount);
            
            var result = await _cifProcessingService.ProcessCifContentAsync(
                sampleContent, runId, request.ForceRefresh);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("X-Correlation-Id", correlationId);
            response.Headers.Add("X-Run-Id", runId);
            
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProcessCifOnDemand failed. RunId: {RunId}", runId);
            
            var errorResult = new CifProcessResult
            {
                ProcessId = runId,
                Status = "failed",
                Errors = new List<string> { ex.Message }
            };
            
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(errorResult);
            return response;
        }
    }

    /// <summary>
    /// HTTP-triggered function to transform a single schedule to event format
    /// For testing and debugging purposes
    /// </summary>
    [Function("TransformCifSchedule")]
    public async Task<HttpResponseData> TransformCifSchedule(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var correlationId = GetCorrelationId(req);
        
        _logger.LogInformation("TransformCifSchedule function started. CorrelationId: {CorrelationId}", correlationId);

        try
        {
            var requestBody = await req.ReadAsStringAsync();
            
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                    "Request body is required");
            }

            var schedule = JsonSerializer.Deserialize<JsonScheduleV1>(requestBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (schedule == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                    "Failed to parse schedule");
            }

            var eventData = _cifProcessingService.TransformToEvent(schedule, correlationId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("X-Correlation-Id", correlationId);
            
            await response.WriteAsJsonAsync(eventData);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TransformCifSchedule failed");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    private static string GetCorrelationId(HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Correlation-Id", out var values))
        {
            return values.FirstOrDefault() ?? Guid.NewGuid().ToString();
        }
        return Guid.NewGuid().ToString();
    }

    private static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        
        await response.WriteAsJsonAsync(new
        {
            error = message,
            statusCode = (int)statusCode,
            timestamp = DateTime.UtcNow
        });
        
        return response;
    }

    /// <summary>
    /// Generate sample CIF content for demo purposes
    /// </summary>
    private static string GenerateSampleCifContent(int recordCount)
    {
        var random = new Random();
        var lines = new List<string>();
        
        // Add header record
        lines.Add(JsonSerializer.Serialize(new CifRecord
        {
            JsonTimetableV1 = new JsonTimetableV1
            {
                Classification = "public",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Owner = "Network Rail"
            }
        }));

        var operators = new[] { "VT", "GR", "GW", "XC", "SR", "NT", "TP", "SE", "AW", "CC" };
        var tiplocCodes = new[]
        {
            "EUSTON", "KNGX", "STPX", "PADTON", "VICTRIA", "BHAM", "MNCRPIC", 
            "LEEDS", "EDINBUR", "GLGC", "BRSTLTM", "CRDFCNT", "YORK", "MKTNKYL"
        };
        var categories = new[] { "OO", "XX", "OW", "XZ", "BR", "EE" };
        var stpIndicators = new[] { "N", "N", "N", "N", "P", "O", "C" }; // Weighted towards N

        for (var i = 0; i < recordCount; i++)
        {
            // Select random locations for the schedule
            var locationCount = random.Next(3, 8);
            var selectedTiplocs = tiplocCodes.OrderBy(_ => random.Next()).Take(locationCount).ToArray();
            
            var locations = new List<ScheduleLocationRecord>();
            var currentTime = new TimeSpan(random.Next(5, 22), random.Next(0, 60), 0);

            for (var j = 0; j < selectedTiplocs.Length; j++)
            {
                var isFirst = j == 0;
                var isLast = j == selectedTiplocs.Length - 1;
                
                locations.Add(new ScheduleLocationRecord
                {
                    tiploc_code = selectedTiplocs[j],
                    location_type = isFirst ? "LO" : (isLast ? "LT" : "LI"),
                    record_identity = isFirst ? "LO" : (isLast ? "LT" : "LI"),
                    departure = isLast ? null : currentTime.ToString(@"hhmm"),
                    arrival = isFirst ? null : currentTime.ToString(@"hhmm"),
                    public_departure = isLast ? null : currentTime.ToString(@"hhmm"),
                    public_arrival = isFirst ? null : currentTime.ToString(@"hhmm"),
                    platform = random.Next(1, 15).ToString()
                });
                
                currentTime = currentTime.Add(TimeSpan.FromMinutes(random.Next(15, 45)));
            }

            var schedule = new JsonScheduleV1
            {
                CIF_train_uid = $"{(char)('A' + random.Next(26))}{random.Next(10000, 99999)}",
                CIF_stp_indicator = stpIndicators[random.Next(stpIndicators.Length)],
                schedule_start_date = DateTime.UtcNow.AddDays(random.Next(0, 14)).ToString("yyyy-MM-dd"),
                schedule_end_date = DateTime.UtcNow.AddMonths(3).ToString("yyyy-MM-dd"),
                schedule_days_runs = "1111100",
                train_status = "P",
                train_category = categories[random.Next(categories.Length)],
                atoc_code = operators[random.Next(operators.Length)],
                applicable_timetable = "Y",
                schedule_location = locations
            };

            lines.Add(JsonSerializer.Serialize(new CifRecord { JsonScheduleV1 = schedule }));
        }

        return string.Join("\n", lines);
    }
}
