using Microsoft.AspNetCore.Mvc;
using Transgrid.MockServer.Models;
using Transgrid.MockServer.Services;

namespace Transgrid.MockServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NetworkRailController : ControllerBase
{
    private readonly DataStore _dataStore;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private static DateTime _lastRunTime = DateTime.MinValue;
    private static int _filesProcessed = 0;
    private static int _totalPublished = 0;
    private static int _totalFiltered = 0;

    public NetworkRailController(DataStore dataStore, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _dataStore = dataStore;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public ActionResult<List<CifSchedule>> GetAll()
    {
        return Ok(_dataStore.GetCifSchedules());
    }

    /// <summary>
    /// Get statistics for the Network Rail demo page
    /// </summary>
    [HttpGet("stats")]
    public ActionResult GetStats()
    {
        var schedules = _dataStore.GetCifSchedules();
        return Ok(new
        {
            filesProcessedLast24Hours = _filesProcessed,
            schedulesPublished = schedules.Count,
            schedulesFiltered = _totalFiltered,
            lastRunTime = _lastRunTime == DateTime.MinValue ? null : (DateTime?)_lastRunTime,
            totalRecords = schedules.Count
        });
    }

    /// <summary>
    /// Simulate CIF file processing workflow
    /// </summary>
    [HttpPost("process")]
    public ActionResult SimulateProcess([FromBody] CifProcessRequest request)
    {
        var startTime = DateTime.UtcNow;
        var random = new Random();
        
        // Calculate how many records to publish vs filter
        var totalRecords = request.RecordCount;
        var filteredCount = (int)(totalRecords * request.FilterRate);
        var publishedCount = totalRecords - filteredCount;

        // Generate new schedules
        var operators = new[] { "Avanti West Coast", "LNER", "GWR", "CrossCountry", "ScotRail", "Northern", "TransPennine", "Southeastern" };
        var powerTypes = new[] { "EMU", "DMU", "Electric", "Diesel", "HST" };
        var trainClasses = new[] { "Express", "Regional", "Commuter", "Intercity", "Local" };

        for (var i = 0; i < publishedCount; i++)
        {
            var schedule = new CifSchedule
            {
                Id = Guid.NewGuid().ToString(),
                TrainServiceNumber = $"{random.Next(1, 9)}{random.Next(100, 999):D3}",
                TravelDate = DateTime.UtcNow.Date.AddDays(random.Next(0, 7)),
                Operator = operators[random.Next(operators.Length)],
                TrainClass = trainClasses[random.Next(trainClasses.Length)],
                PowerType = powerTypes[random.Next(powerTypes.Length)],
                TrainCategory = "OO",
                CifStpIndicator = "N",
                ValidFrom = DateTime.UtcNow.Date,
                ValidTo = DateTime.UtcNow.Date.AddMonths(3),
                CreatedAt = DateTime.UtcNow,
                ScheduleLocations = GenerateScheduleLocations(random)
            };
            
            _dataStore.AddCifSchedule(schedule);
        }

        _lastRunTime = DateTime.UtcNow;
        _filesProcessed++;
        _totalPublished += publishedCount;
        _totalFiltered += filteredCount;

        return Ok(new
        {
            success = true,
            processingType = request.ProcessingType,
            totalRecords,
            publishedCount,
            filteredCount,
            fileSize = $"{(totalRecords * 0.5):F1} KB",
            duration = $"{(DateTime.UtcNow - startTime).TotalSeconds:F2}s"
        });
    }

    /// <summary>
    /// Simulate CIF file download
    /// </summary>
    [HttpPost("download")]
    public ActionResult SimulateDownload()
    {
        var random = new Random();
        var recordCount = random.Next(50, 200);
        
        return Ok(new
        {
            fileName = $"toc-full.CIF.gz",
            fileSize = $"{(recordCount * 0.5):F1} KB",
            recordCount,
            timestamp = DateTime.UtcNow.ToString("O"),
            source = "https://datafeeds.networkrail.co.uk/ntrod/CifFileAuthenticate"
        });
    }

    /// <summary>
    /// Call the real Azure Function for CIF processing
    /// </summary>
    [HttpPost("process-azure")]
    public async Task<ActionResult> ProcessViaAzureFunction([FromBody] AzureCifProcessRequest request)
    {
        var functionEndpoint = _configuration["FunctionDebug:FunctionBaseUrl"] 
                            ?? _configuration["AzureFunctionEndpoint"];
        var functionKey = _configuration["FunctionDebug:FunctionKey"] 
                       ?? _configuration["FUNCTION_KEY"];
        
        if (string.IsNullOrEmpty(functionEndpoint))
        {
            return BadRequest(new { error = "Azure Function endpoint not configured. Set FunctionDebug:FunctionBaseUrl in appsettings.json" });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            
            var requestBody = new
            {
                fileType = request.FileType ?? "update",
                forceRefresh = request.ForceRefresh
            };

            var correlationId = Guid.NewGuid().ToString();
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);

            // Build URL with function key if available
            var functionUrl = $"{functionEndpoint.TrimEnd('/')}/api/ProcessCifOnDemand";
            if (!string.IsNullOrEmpty(functionKey))
            {
                functionUrl += $"?code={functionKey}";
            }

            var response = await client.PostAsJsonAsync(functionUrl, requestBody);

            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, new
                {
                    error = "Azure Function returned an error",
                    statusCode = (int)response.StatusCode,
                    correlationId,
                    details = content
                });
            }

            return Content(content, "application/json");
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new
            {
                error = "Failed to connect to Azure Function",
                message = ex.Message,
                hint = "Make sure the Azure Function is deployed and running"
            });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new
            {
                error = "Request timed out",
                message = "The Azure Function did not respond within the timeout period"
            });
        }
    }

    /// <summary>
    /// Transform a single CIF schedule via Azure Function
    /// </summary>
    [HttpPost("transform-azure")]
    public async Task<ActionResult> TransformViaAzureFunction([FromBody] object cifSchedule)
    {
        var functionEndpoint = _configuration["FunctionDebug:FunctionBaseUrl"] 
                            ?? _configuration["AzureFunctionEndpoint"];
        var functionKey = _configuration["FunctionDebug:FunctionKey"] 
                       ?? _configuration["FUNCTION_KEY"];
        
        if (string.IsNullOrEmpty(functionEndpoint))
        {
            return BadRequest(new { error = "Azure Function endpoint not configured" });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            
            // Build URL with function key if available
            var functionUrl = $"{functionEndpoint.TrimEnd('/')}/api/TransformCifSchedule";
            if (!string.IsNullOrEmpty(functionKey))
            {
                functionUrl += $"?code={functionKey}";
            }
            
            var response = await client.PostAsJsonAsync(functionUrl, cifSchedule);

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to call Azure Function", message = ex.Message });
        }
    }

    /// <summary>
    /// Get the Azure Function configuration status
    /// </summary>
    [HttpGet("azure-status")]
    public ActionResult GetAzureFunctionStatus()
    {
        var functionEndpoint = _configuration["FunctionDebug:FunctionBaseUrl"] 
                            ?? _configuration["AzureFunctionEndpoint"];
        var functionKey = _configuration["FunctionDebug:FunctionKey"] 
                       ?? _configuration["FUNCTION_KEY"];
        var hasKey = !string.IsNullOrEmpty(functionKey);
        
        return Ok(new
        {
            configured = !string.IsNullOrEmpty(functionEndpoint),
            hasKey,
            endpoint = string.IsNullOrEmpty(functionEndpoint) ? null : $"{functionEndpoint.TrimEnd('/')}/api/...",
            message = string.IsNullOrEmpty(functionEndpoint)
                ? "Azure Function endpoint not configured. Set 'FunctionDebug:FunctionBaseUrl' in appsettings.json"
                : hasKey 
                    ? "Azure Function endpoint and key are configured"
                    : "Azure Function endpoint configured but no function key - calls may fail with 401/404"
        });
    }

    /// <summary>
    /// Clear all schedule data
    /// </summary>
    [HttpPost("clear")]
    public ActionResult ClearSchedules()
    {
        var schedules = _dataStore.GetCifSchedules().ToList();
        foreach (var schedule in schedules)
        {
            _dataStore.DeleteCifSchedule(schedule.Id);
        }

        _totalFiltered = 0;
        _totalPublished = 0;

        return Ok(new { message = "All schedules cleared", count = schedules.Count });
    }

    private List<ScheduleLocation> GenerateScheduleLocations(Random random)
    {
        var stations = new[]
        {
            ("EUSTON", "London Euston"),
            ("KNGX", "London King's Cross"),
            ("PADTON", "London Paddington"),
            ("VICTRIA", "London Victoria"),
            ("STPX", "London St Pancras"),
            ("BHAM", "Birmingham New Street"),
            ("MNCRPIC", "Manchester Piccadilly"),
            ("LEEDS", "Leeds"),
            ("EDINBUR", "Edinburgh Waverley"),
            ("GLGC", "Glasgow Central"),
            ("BRSTLTM", "Bristol Temple Meads"),
            ("CRDFCNT", "Cardiff Central")
        };

        var count = random.Next(3, 8);
        var selectedStations = stations.OrderBy(_ => random.Next()).Take(count).ToList();
        var locations = new List<ScheduleLocation>();
        var currentTime = new TimeSpan(random.Next(5, 22), random.Next(0, 60), 0);

        for (var i = 0; i < selectedStations.Count; i++)
        {
            var station = selectedStations[i];
            var isFirst = i == 0;
            var isLast = i == selectedStations.Count - 1;

            locations.Add(new ScheduleLocation
            {
                LocationCode = station.Item1,
                LocationName = station.Item2,
                ScheduledArrivalTime = isFirst ? string.Empty : currentTime.ToString(@"hh\:mm"),
                ScheduledDepartureTime = isLast ? string.Empty : currentTime.Add(TimeSpan.FromMinutes(random.Next(1, 5))).ToString(@"hh\:mm"),
                Platform = random.Next(1, 15).ToString(),
                Activity = isFirst ? "TB" : (isLast ? "TF" : "T")
            });

            currentTime = currentTime.Add(TimeSpan.FromMinutes(random.Next(15, 45)));
        }

        return locations;
    }

    [HttpGet("{id}")]
    public ActionResult<CifSchedule> Get(string id)
    {
        var schedule = _dataStore.GetCifSchedule(id);
        if (schedule == null)
        {
            return NotFound();
        }
        return Ok(schedule);
    }

    [HttpPost]
    public ActionResult<CifSchedule> Create([FromBody] CifSchedule? schedule)
    {
        if (schedule == null)
        {
            return BadRequest("CIF schedule data is required");
        }
        
        if (string.IsNullOrWhiteSpace(schedule.TrainServiceNumber))
        {
            return BadRequest("Train service number is required");
        }
        
        schedule.Id = Guid.NewGuid().ToString();
        schedule.CreatedAt = DateTime.UtcNow;
        _dataStore.AddCifSchedule(schedule);
        return CreatedAtAction(nameof(Get), new { id = schedule.Id }, schedule);
    }

    [HttpPut("{id}")]
    public ActionResult Update(string id, [FromBody] CifSchedule? schedule)
    {
        if (schedule == null)
        {
            return BadRequest("CIF schedule data is required");
        }
        
        if (string.IsNullOrWhiteSpace(schedule.TrainServiceNumber))
        {
            return BadRequest("Train service number is required");
        }
        
        var existing = _dataStore.GetCifSchedule(id);
        if (existing == null)
        {
            return NotFound();
        }
        schedule.Id = id;
        schedule.CreatedAt = existing.CreatedAt;
        _dataStore.UpdateCifSchedule(schedule);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(string id)
    {
        var existing = _dataStore.GetCifSchedule(id);
        if (existing == null)
        {
            return NotFound();
        }
        _dataStore.DeleteCifSchedule(id);
        return NoContent();
    }
}
