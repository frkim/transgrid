using Microsoft.AspNetCore.Mvc;
using Transgrid.MockServer.Models;
using Transgrid.MockServer.Services;

namespace Transgrid.MockServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesforceController : ControllerBase
{
    private readonly DataStore _dataStore;
    private readonly ServiceBusService _serviceBusService;
    private readonly ILogger<SalesforceController> _logger;
    private static DateTime _lastExtractTime = DateTime.MinValue;
    private static int _totalExtracts = 0;

    public SalesforceController(DataStore dataStore, ServiceBusService serviceBusService, ILogger<SalesforceController> logger)
    {
        _dataStore = dataStore;
        _serviceBusService = serviceBusService;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<List<NegotiatedRate>> GetAll()
    {
        return Ok(_dataStore.GetNegotiatedRates());
    }

    /// <summary>
    /// Get statistics for the Salesforce demo page
    /// </summary>
    [HttpGet("stats")]
    public ActionResult GetStats()
    {
        var rates = _dataStore.GetNegotiatedRates();
        return Ok(new
        {
            totalRecords = rates.Count,
            pendingRecords = rates.Count(r => r.B2bStatus == "Pending"),
            extractedRecords = rates.Count(r => r.B2bStatus == "Extracted"),
            failedRecords = rates.Count(r => r.B2bStatus == "Failed"),
            lastExtractTime = _lastExtractTime == DateTime.MinValue ? null : (DateTime?)_lastExtractTime,
            totalExtracts = _totalExtracts
        });
    }

    /// <summary>
    /// Simulate the Salesforce negotiated rates extract workflow
    /// </summary>
    [HttpPost("extract")]
    public ActionResult SimulateExtract([FromBody] SalesforceExtractRequest request)
    {
        var startTime = DateTime.UtcNow;
        var rates = _dataStore.GetNegotiatedRates()
            .Where(r => r.B2bStatus == "Pending" || r.ExtractRequested)
            .ToList();

        // Apply priority filter
        if (request.Priority != "ALL")
        {
            rates = rates.Where(r => r.Priority == request.Priority).ToList();
        }

        var random = new Random();
        var routes = new List<object>();
        var successCount = 0;
        var failedCount = 0;

        // Process each route
        var routesToProcess = request.ExtractRoute == "ALL" 
            ? new[] { "IDL_S3", "GDS_AIR", "BENE" } 
            : new[] { request.ExtractRoute };

        foreach (var route in routesToProcess)
        {
            var routeRates = route switch
            {
                "IDL_S3" => rates.Where(r => r.CodeRecordType.Contains("GND") || r.CodeRecordType.Contains("IDL")).ToList(),
                "GDS_AIR" => rates.Where(r => r.GdsUsed != null).ToList(),
                "BENE" => rates.Where(r => r.Distributor != null).ToList(),
                _ => rates
            };

            var routeSuccess = !request.SimulateFailures || random.Next(100) > 20;
            var csvPreview = GenerateCsvPreview(route, routeRates.Take(3).ToList());

            routes.Add(new
            {
                routeName = route switch
                {
                    "IDL_S3" => "IDL/S3 (Internal)",
                    "GDS_AIR" => "GDS Air (Travel Agents)",
                    "BENE" => "BeNe (External)",
                    _ => route
                },
                success = routeSuccess,
                recordCount = routeRates.Count,
                fileName = $"NegotiatedRates_{route}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv",
                csvPreview = csvPreview
            });

            if (routeSuccess)
            {
                successCount += routeRates.Count;
                foreach (var rate in routeRates)
                {
                    rate.B2bStatus = "Extracted";
                    rate.B2bExtractDate = DateTime.UtcNow;
                    _dataStore.UpdateNegotiatedRate(rate);
                }
            }
            else
            {
                failedCount += routeRates.Count;
                foreach (var rate in routeRates)
                {
                    rate.B2bStatus = "Failed";
                    _dataStore.UpdateNegotiatedRate(rate);
                }
            }
        }

        _lastExtractTime = DateTime.UtcNow;
        _totalExtracts++;

        return Ok(new
        {
            success = true,
            totalRecords = rates.Count,
            successCount,
            failedCount,
            routes,
            duration = $"{(DateTime.UtcNow - startTime).TotalSeconds:F2}s"
        });
    }

    /// <summary>
    /// Simulate a Salesforce Platform Event and publish to Service Bus
    /// </summary>
    [HttpPost("platform-event")]
    public async Task<ActionResult> SimulatePlatformEvent([FromBody] PlatformEventRequest request)
    {
        var rates = _dataStore.GetNegotiatedRates()
            .Where(r => r.B2bStatus == "Pending")
            .ToList();

        foreach (var rate in rates)
        {
            rate.ExtractRequested = true;
            _dataStore.UpdateNegotiatedRate(rate);
        }

        var eventType = request.EventType ?? "NegotiatedRateExtract__e";
        var rateIds = rates.Select(r => r.Id).ToList();

        // Publish to Service Bus
        var publishResult = await _serviceBusService.PublishPlatformEventAsync(eventType, rateIds);

        _logger.LogInformation("Platform event published: {EventType} with {Count} records. Simulated: {IsSimulated}", 
            eventType, rates.Count, publishResult.IsSimulated);

        return Ok(new
        {
            eventType,
            recordCount = rates.Count,
            timestamp = DateTime.UtcNow.ToString("O"),
            serviceBusPublished = publishResult.Success,
            isSimulated = publishResult.IsSimulated,
            correlationId = publishResult.CorrelationId,
            errorMessage = publishResult.ErrorMessage
        });
    }

    /// <summary>
    /// Get negotiated rates by IDs (called by Logic Apps workflow)
    /// </summary>
    [HttpPost("getNegotiatedRates")]
    public ActionResult<List<NegotiatedRate>> GetNegotiatedRatesByIds([FromBody] NegotiatedRatesRequest request)
    {
        if (request?.Ids == null || !request.Ids.Any())
        {
            // Return all pending rates if no IDs specified
            return Ok(_dataStore.GetNegotiatedRates().Where(r => r.ExtractRequested || r.B2bStatus == "Pending").ToList());
        }

        var rates = _dataStore.GetNegotiatedRates()
            .Where(r => request.Ids.Contains(r.Id))
            .ToList();

        return Ok(rates);
    }

    /// <summary>
    /// Update extract status (called by Logic Apps workflow)
    /// </summary>
    [HttpPost("updateExtractStatus")]
    public ActionResult UpdateExtractStatus([FromBody] UpdateExtractStatusRequest request)
    {
        if (request?.Ids == null || !request.Ids.Any())
        {
            return BadRequest("No IDs provided");
        }

        var rates = _dataStore.GetNegotiatedRates()
            .Where(r => request.Ids.Contains(r.Id))
            .ToList();

        foreach (var rate in rates)
        {
            rate.B2bStatus = request.Status ?? "Extracted";
            rate.B2bExtractDate = request.ExtractDate ?? DateTime.UtcNow;
            rate.ExtractRequested = false;
            _dataStore.UpdateNegotiatedRate(rate);
        }

        _lastExtractTime = DateTime.UtcNow;
        _totalExtracts++;

        return Ok(new
        {
            updatedCount = rates.Count,
            status = request.Status,
            extractDate = request.ExtractDate
        });
    }

    /// <summary>
    /// Reset all records to Pending status
    /// </summary>
    [HttpPost("reset")]
    public ActionResult ResetStatus()
    {
        var rates = _dataStore.GetNegotiatedRates();
        foreach (var rate in rates)
        {
            rate.B2bStatus = "Pending";
            rate.B2bExtractDate = null;
            rate.ExtractRequested = false;
            _dataStore.UpdateNegotiatedRate(rate);
        }

        return Ok(new { message = "All records reset to Pending", count = rates.Count });
    }

    private string GenerateCsvPreview(string route, List<NegotiatedRate> rates)
    {
        var header = route switch
        {
            "IDL_S3" => "Account Manager,Account Name,Unique Code,Type,Road,Tariff Codes,Discounts,Action Type",
            "GDS_AIR" => "Account Manager,Account Name,Unique Code,GDS Used,PCC,Road,Tariff Codes,Action Type",
            "BENE" => "Account Manager,Account Name,Unique Code,Distributor,Road,Tariff Codes,Action Type",
            _ => "Account Manager,Account Name,Unique Code,Type,Road,Action Type"
        };

        var lines = new List<string> { header };
        foreach (var rate in rates)
        {
            var line = route switch
            {
                "IDL_S3" => $"{rate.AccountManager},{rate.AccountName},{rate.UniqueCode},{rate.CodeRecordType},{rate.Road},{string.Join("|", rate.TariffCodes)},{string.Join("|", rate.Discounts.Values.Select(v => $"{v}%"))},{rate.ActionType}",
                "GDS_AIR" => $"{rate.AccountManager},{rate.AccountName},{rate.UniqueCode},{rate.GdsUsed},{rate.Pcc},{rate.Road},{string.Join("|", rate.TariffCodes)},{rate.ActionType}",
                "BENE" => $"{rate.AccountManager},{rate.AccountName},{rate.UniqueCode},{rate.Distributor},{rate.Road},{string.Join("|", rate.TariffCodes)},{rate.ActionType}",
                _ => $"{rate.AccountManager},{rate.AccountName},{rate.UniqueCode},{rate.CodeRecordType},{rate.Road},{rate.ActionType}"
            };
            lines.Add(line);
        }

        return string.Join("\n", lines);
    }

    [HttpGet("{id}")]
    public ActionResult<NegotiatedRate> Get(string id)
    {
        var rate = _dataStore.GetNegotiatedRate(id);
        if (rate == null)
        {
            return NotFound();
        }
        return Ok(rate);
    }

    [HttpPost]
    public ActionResult<NegotiatedRate> Create([FromBody] NegotiatedRate? rate)
    {
        if (rate == null)
        {
            return BadRequest("Negotiated rate data is required");
        }
        
        if (string.IsNullOrWhiteSpace(rate.UniqueCode))
        {
            return BadRequest("Unique code is required");
        }
        
        rate.Id = Guid.NewGuid().ToString();
        rate.CreatedAt = DateTime.UtcNow;
        _dataStore.AddNegotiatedRate(rate);
        return CreatedAtAction(nameof(Get), new { id = rate.Id }, rate);
    }

    [HttpPut("{id}")]
    public ActionResult Update(string id, [FromBody] NegotiatedRate? rate)
    {
        if (rate == null)
        {
            return BadRequest("Negotiated rate data is required");
        }
        
        if (string.IsNullOrWhiteSpace(rate.UniqueCode))
        {
            return BadRequest("Unique code is required");
        }
        
        var existing = _dataStore.GetNegotiatedRate(id);
        if (existing == null)
        {
            return NotFound();
        }
        rate.Id = id;
        rate.CreatedAt = existing.CreatedAt;
        _dataStore.UpdateNegotiatedRate(rate);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(string id)
    {
        var existing = _dataStore.GetNegotiatedRate(id);
        if (existing == null)
        {
            return NotFound();
        }
        _dataStore.DeleteNegotiatedRate(id);
        return NoContent();
    }
}
