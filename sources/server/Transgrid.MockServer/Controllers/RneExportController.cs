using Microsoft.AspNetCore.Mvc;
using Transgrid.MockServer.Models;
using Transgrid.MockServer.Services;

namespace Transgrid.MockServer.Controllers;

/// <summary>
/// Controller for simulating RNE (Rail Net Europe) operational plan exports.
/// Provides endpoints for triggering exports, viewing history, and managing failed exports.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RneExportController : ControllerBase
{
    private readonly DataStore _dataStore;
    private static readonly List<RneExportRun> _exportHistory = new();
    private static readonly List<FailedExportRecord> _failedExports = new();
    private static readonly Random _random = new();
    private static readonly object _lock = new();

    public RneExportController(DataStore dataStore)
    {
        _dataStore = dataStore;
    }

    /// <summary>
    /// Simulate an export run for the specified travel date.
    /// </summary>
    /// <param name="request">The export simulation request</param>
    /// <returns>The export run results</returns>
    [HttpPost("simulate")]
    public ActionResult<RneExportRun> SimulateExport([FromBody] SimulateExportRequest request)
    {
        var startTime = DateTime.UtcNow;
        var travelDate = request.TravelDate ?? DateTime.Today;

        // Get train plans matching RNE criteria
        var trainPlans = _dataStore.GetTrainPlans()
            .Where(p => 
                (p.Country == "FR" || p.Country == "GB") &&  // France or UK only
                p.Status == "ACTIVE" &&                       // Active plans only
                p.PlanType != "EVOLUTION" &&                  // Exclude evolutions
                p.TravelDate.Date == travelDate.Date)
            .ToList();

        var exportRun = new RneExportRun
        {
            TravelDate = travelDate,
            ExportType = request.ExportType,
            TotalPlans = trainPlans.Count,
            Status = "IN_PROGRESS"
        };

        var results = new List<RneExportResult>();

        foreach (var plan in trainPlans)
        {
            var result = ProcessTrainPlan(plan, request.SimulateFailures, request.FailureRate);
            results.Add(result);

            if (result.Success)
            {
                exportRun.SuccessfulExports++;
            }
            else
            {
                exportRun.FailedExports++;
                
                // Add to failed exports for retry
                lock (_lock)
                {
                    _failedExports.Add(new FailedExportRecord
                    {
                        TrainId = plan.Id,
                        ServiceCode = plan.ServiceCode,
                        TravelDate = plan.TravelDate,
                        FailureReason = result.ErrorMessage ?? "Unknown error"
                    });
                }
            }
        }

        exportRun.Results = results;
        exportRun.Duration = DateTime.UtcNow - startTime;
        exportRun.Status = exportRun.FailedExports == 0 ? "COMPLETED" : "COMPLETED_WITH_ERRORS";

        lock (_lock)
        {
            _exportHistory.Insert(0, exportRun);
            
            // Keep only last 50 export runs
            if (_exportHistory.Count > 50)
            {
                _exportHistory.RemoveRange(50, _exportHistory.Count - 50);
            }
        }

        return Ok(exportRun);
    }

    /// <summary>
    /// Get the list of failed exports pending retry.
    /// </summary>
    [HttpGet("failed")]
    public ActionResult<List<FailedExportRecord>> GetFailedExports()
    {
        lock (_lock)
        {
            return Ok(_failedExports.Where(f => f.RetryCount < 3).OrderByDescending(f => f.CreatedAt).ToList());
        }
    }

    /// <summary>
    /// Retry failed exports.
    /// </summary>
    [HttpPost("retry")]
    public ActionResult<RneExportRun> RetryFailedExports()
    {
        var startTime = DateTime.UtcNow;

        List<FailedExportRecord> toRetry;
        lock (_lock)
        {
            toRetry = _failedExports.Where(f => f.RetryCount < 3).ToList();
        }

        if (toRetry.Count == 0)
        {
            return Ok(new RneExportRun
            {
                ExportType = "RETRY",
                TotalPlans = 0,
                Status = "COMPLETED"
            });
        }

        var exportRun = new RneExportRun
        {
            TravelDate = toRetry.First().TravelDate,
            ExportType = "RETRY",
            TotalPlans = toRetry.Count,
            Status = "IN_PROGRESS"
        };

        var results = new List<RneExportResult>();

        foreach (var failed in toRetry)
        {
            var plan = _dataStore.GetTrainPlan(failed.TrainId);
            if (plan == null)
            {
                failed.RetryCount++;
                failed.LastRetryAt = DateTime.UtcNow;
                continue;
            }

            // Retry with lower failure rate (simulate improving conditions)
            var result = ProcessTrainPlan(plan, true, 0.3);
            results.Add(result);

            lock (_lock)
            {
                if (result.Success)
                {
                    exportRun.SuccessfulExports++;
                    _failedExports.Remove(failed);
                }
                else
                {
                    exportRun.FailedExports++;
                    failed.RetryCount++;
                    failed.LastRetryAt = DateTime.UtcNow;
                    failed.FailureReason = result.ErrorMessage ?? "Retry failed";
                }
            }
        }

        exportRun.Results = results;
        exportRun.Duration = DateTime.UtcNow - startTime;
        exportRun.Status = "COMPLETED";

        lock (_lock)
        {
            _exportHistory.Insert(0, exportRun);
        }

        return Ok(exportRun);
    }

    /// <summary>
    /// Get export history.
    /// </summary>
    [HttpGet("history")]
    public ActionResult<List<RneExportRun>> GetExportHistory([FromQuery] int limit = 10)
    {
        lock (_lock)
        {
            return Ok(_exportHistory.Take(limit).ToList());
        }
    }

    /// <summary>
    /// Get export statistics.
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<object> GetStats()
    {
        lock (_lock)
        {
            var last24Hours = _exportHistory.Where(r => r.ExecutedAt >= DateTime.UtcNow.AddHours(-24)).ToList();
            var lastWeek = _exportHistory.Where(r => r.ExecutedAt >= DateTime.UtcNow.AddDays(-7)).ToList();

            return Ok(new
            {
                totalExportsLast24Hours = last24Hours.Count,
                successfulLast24Hours = last24Hours.Sum(r => r.SuccessfulExports),
                failedLast24Hours = last24Hours.Sum(r => r.FailedExports),
                totalExportsLastWeek = lastWeek.Count,
                pendingRetries = _failedExports.Count(f => f.RetryCount < 3),
                averageDurationMs = last24Hours.Any() ? last24Hours.Average(r => r.Duration.TotalMilliseconds) : 0
            });
        }
    }

    /// <summary>
    /// Clear all failed exports (for demo reset).
    /// </summary>
    [HttpPost("clear-failed")]
    public ActionResult ClearFailedExports()
    {
        lock (_lock)
        {
            _failedExports.Clear();
        }
        return Ok(new { message = "Failed exports cleared" });
    }

    /// <summary>
    /// Clear export history (for demo reset).
    /// </summary>
    [HttpPost("clear-history")]
    public ActionResult ClearHistory()
    {
        lock (_lock)
        {
            _exportHistory.Clear();
        }
        return Ok(new { message = "Export history cleared" });
    }

    private RneExportResult ProcessTrainPlan(TrainPlan plan, bool simulateFailures, double failureRate)
    {
        var shouldFail = simulateFailures && _random.NextDouble() < failureRate;

        if (shouldFail)
        {
            var errors = new[]
            {
                "SFTP connection timeout",
                "XML validation failed: missing required element",
                "Blob storage write error",
                "Transform function returned null",
                "Network connectivity issue"
            };

            return new RneExportResult
            {
                TrainId = plan.Id,
                ServiceCode = plan.ServiceCode,
                TravelDate = plan.TravelDate,
                Success = false,
                ErrorMessage = errors[_random.Next(errors.Length)]
            };
        }

        // Generate mock XML preview
        var xmlPreview = GenerateXmlPreview(plan);
        var blobPath = $"ci-rne-export/{plan.TravelDate:yyyy-MM}/{plan.TravelDate:yyyy-MM-dd}/{plan.ServiceCode}.xml";

        return new RneExportResult
        {
            TrainId = plan.Id,
            ServiceCode = plan.ServiceCode,
            TravelDate = plan.TravelDate,
            Success = true,
            BlobPath = blobPath,
            UploadedToPrimary = true,
            UploadedToBackup = true,
            XmlPreview = xmlPreview
        };
    }

    private string GenerateXmlPreview(TrainPlan plan)
    {
        var passagePointsXml = string.Join("\n        ", plan.PassagePoints.Select((pp, i) => 
            $"<PassagePoint><LocationCode>{pp}</LocationCode><Sequence>{i + 1}</Sequence></PassagePoint>"));

        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<PassengerTrainCompositionProcessMessage 
  xmlns=""http://taf-jsg.info/schemas/2.1.6/ptcpm"">
  <MessageHeader>
    <MessageIdentifier>{Guid.NewGuid()}</MessageIdentifier>
    <MessageType>PTCPMRequest</MessageType>
    <SenderReference>EUROSTAR</SenderReference>
    <RecipientReference>RNE</RecipientReference>
    <MessageDateTime>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</MessageDateTime>
  </MessageHeader>
  <TrainInformation>
    <TrainNumber>{plan.ServiceCode}</TrainNumber>
    <TravelDate>{plan.TravelDate:yyyy-MM-dd}</TravelDate>
    <OperationalPathway>{plan.Pathway}</OperationalPathway>
    <Origin>{plan.Origin}</Origin>
    <Destination>{plan.Destination}</Destination>
    <PassagePoints>
      {passagePointsXml}
    </PassagePoints>
  </TrainInformation>
</PassengerTrainCompositionProcessMessage>";
    }
}
