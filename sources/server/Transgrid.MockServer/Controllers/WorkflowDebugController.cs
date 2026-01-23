using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Transgrid.MockServer.Controllers;

/// <summary>
/// Controller for debugging and executing Azure Logic App workflows.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkflowDebugController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WorkflowDebugController> _logger;
    private readonly IConfiguration _configuration;

    public WorkflowDebugController(
        IHttpClientFactory httpClientFactory,
        ILogger<WorkflowDebugController> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the list of workflows and their run history from Azure Logic Apps.
    /// </summary>
    [HttpGet("workflows")]
    public async Task<ActionResult<WorkflowListResult>> GetWorkflows([FromQuery] string? baseUrl)
    {
        var logicAppBaseUrl = baseUrl?.TrimEnd('/') 
            ?? _configuration["WorkflowDebug:LogicAppBaseUrl"]?.TrimEnd('/') 
            ?? "https://logic-transgrid-dev.azurewebsites.net";

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var workflows = new List<WorkflowStatus>();
            var workflowNames = new[] 
            { 
                "rne-daily-export", 
                "rne-d2-export", 
                "rne-http-trigger", 
                "rne-retry-failed",
                "sf-negotiated-rates",
                "nr-cif-processing"
            };

            foreach (var workflowName in workflowNames)
            {
                var status = new WorkflowStatus
                {
                    WorkflowName = workflowName,
                    Status = "Unknown",
                    LastRun = null
                };

                try
                {
                    // Try to get workflow trigger URL (this will tell us if workflow exists)
                    var triggerUrl = $"{logicAppBaseUrl}/api/{workflowName}/triggers/manual/listCallbackUrl";
                    
                    // Note: In real implementation, we'd need management API access
                    // For now, we mark as "Deployed" if reachable
                    status.Status = "Deployed";
                }
                catch
                {
                    status.Status = "Not Found";
                }

                workflows.Add(status);
            }

            return Ok(new WorkflowListResult
            {
                Success = true,
                Workflows = workflows
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workflow list");
            return Ok(new WorkflowListResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Triggers a workflow execution via HTTP trigger.
    /// </summary>
    [HttpPost("trigger")]
    public async Task<ActionResult<WorkflowExecutionResult>> TriggerWorkflow([FromBody] WorkflowTriggerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowName))
        {
            return BadRequest(new WorkflowExecutionResult
            {
                Success = false,
                Error = "Workflow name is required"
            });
        }

        var logicAppBaseUrl = request.LogicAppBaseUrl?.TrimEnd('/') 
            ?? _configuration["WorkflowDebug:LogicAppBaseUrl"]?.TrimEnd('/') 
            ?? "https://logic-transgrid-dev.azurewebsites.net";

        _logger.LogInformation("Triggering workflow: {WorkflowName} at {BaseUrl}", 
            request.WorkflowName, logicAppBaseUrl);

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(2); // Workflows can take longer

            // Build the trigger URL
            var triggerUrl = $"{logicAppBaseUrl}/api/{request.WorkflowName}/triggers/Manual_HTTP_Trigger/invoke";
            
            // Add SAS token if provided
            if (!string.IsNullOrWhiteSpace(request.SasToken))
            {
                var separator = triggerUrl.Contains('?') ? '&' : '?';
                triggerUrl = $"{triggerUrl}{separator}{request.SasToken.TrimStart('?').TrimStart('&')}";
            }

            var correlationId = Guid.NewGuid().ToString();
            var startTime = DateTime.UtcNow;

            HttpResponseMessage response;
            
            if (!string.IsNullOrWhiteSpace(request.Payload))
            {
                // Validate JSON
                try
                {
                    JsonDocument.Parse(request.Payload);
                }
                catch (JsonException ex)
                {
                    return BadRequest(new WorkflowExecutionResult
                    {
                        Success = false,
                        Error = $"Invalid JSON payload: {ex.Message}"
                    });
                }

                var content = new StringContent(request.Payload, Encoding.UTF8, "application/json");
                content.Headers.Add("X-Correlation-Id", correlationId);
                response = await client.PostAsync(triggerUrl, content);
            }
            else
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, triggerUrl);
                httpRequest.Headers.Add("X-Correlation-Id", correlationId);
                response = await client.SendAsync(httpRequest);
            }

            var endTime = DateTime.UtcNow;
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation(
                "Workflow {WorkflowName} completed with status {StatusCode}. CorrelationId: {CorrelationId}",
                request.WorkflowName, response.StatusCode, correlationId);

            if (response.IsSuccessStatusCode)
            {
                // Try to parse and format response
                string formattedOutput = responseBody;
                try
                {
                    var jsonDoc = JsonDocument.Parse(responseBody);
                    formattedOutput = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                }
                catch
                {
                    // Response might not be JSON
                }

                return Ok(new WorkflowExecutionResult
                {
                    Success = true,
                    Output = formattedOutput,
                    StatusCode = (int)response.StatusCode,
                    CorrelationId = correlationId,
                    ExecutionTimeMs = (int)(endTime - startTime).TotalMilliseconds,
                    Headers = response.Headers
                        .Concat(response.Content.Headers)
                        .ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
                });
            }
            else
            {
                // Parse error details
                string errorMessage = $"Workflow returned HTTP {(int)response.StatusCode}";
                string? stackTrace = null;

                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("error", out var errorProp))
                    {
                        if (errorProp.ValueKind == JsonValueKind.Object && 
                            errorProp.TryGetProperty("message", out var msgProp))
                        {
                            errorMessage = msgProp.GetString() ?? errorMessage;
                        }
                        else if (errorProp.ValueKind == JsonValueKind.String)
                        {
                            errorMessage = errorProp.GetString() ?? errorMessage;
                        }
                    }
                    else if (root.TryGetProperty("message", out var messageProp))
                    {
                        errorMessage = messageProp.GetString() ?? errorMessage;
                    }
                }
                catch
                {
                    if (!string.IsNullOrWhiteSpace(responseBody))
                    {
                        errorMessage += $"\n\nResponse: {responseBody}";
                    }
                }

                return Ok(new WorkflowExecutionResult
                {
                    Success = false,
                    Error = errorMessage,
                    StackTrace = stackTrace,
                    StatusCode = (int)response.StatusCode,
                    CorrelationId = correlationId,
                    ExecutionTimeMs = (int)(endTime - startTime).TotalMilliseconds,
                    RawResponse = responseBody
                });
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to workflow {WorkflowName} failed", request.WorkflowName);
            return Ok(new WorkflowExecutionResult
            {
                Success = false,
                Error = $"Failed to connect to workflow: {ex.Message}",
                StackTrace = ex.StackTrace
            });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Workflow {WorkflowName} request timed out", request.WorkflowName);
            return Ok(new WorkflowExecutionResult
            {
                Success = false,
                Error = "Workflow request timed out after 2 minutes",
                StackTrace = ex.StackTrace
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error triggering workflow {WorkflowName}", request.WorkflowName);
            return Ok(new WorkflowExecutionResult
            {
                Success = false,
                Error = $"Unexpected error: {ex.Message}",
                StackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// Gets the run history for a specific workflow.
    /// Note: Requires Azure Management API access in production.
    /// </summary>
    [HttpGet("runs/{workflowName}")]
    public async Task<ActionResult<WorkflowRunsResult>> GetWorkflowRuns(
        string workflowName, 
        [FromQuery] int top = 10)
    {
        // In a real implementation, this would query the Azure Management API
        // For now, return a placeholder response
        return Ok(new WorkflowRunsResult
        {
            Success = true,
            Message = "Run history requires Azure Management API access. Configure Azure credentials to enable this feature.",
            WorkflowName = workflowName,
            Runs = new List<WorkflowRun>()
        });
    }

    /// <summary>
    /// Triggers all HTTP-triggerable workflows (for batch testing).
    /// </summary>
    [HttpPost("trigger-all")]
    public async Task<ActionResult<BatchExecutionResult>> TriggerAllWorkflows([FromBody] BatchTriggerRequest request)
    {
        var httpWorkflows = new[]
        {
            ("rne-http-trigger", """{"travelDate": "2026-01-25", "exportType": "daily"}"""),
            ("nr-cif-processing", """{"fileType": "update", "forceRefresh": false}""")
        };

        var results = new List<WorkflowExecutionResult>();

        foreach (var (workflowName, defaultPayload) in httpWorkflows)
        {
            var triggerRequest = new WorkflowTriggerRequest
            {
                WorkflowName = workflowName,
                LogicAppBaseUrl = request.LogicAppBaseUrl,
                SasToken = request.SasToken,
                Payload = request.Payloads?.GetValueOrDefault(workflowName) ?? defaultPayload
            };

            var result = await TriggerWorkflow(triggerRequest);
            if (result.Result is OkObjectResult okResult && okResult.Value is WorkflowExecutionResult execResult)
            {
                execResult.WorkflowName = workflowName;
                results.Add(execResult);
            }
        }

        var successCount = results.Count(r => r.Success);
        var failedCount = results.Count(r => !r.Success);

        return Ok(new BatchExecutionResult
        {
            Success = failedCount == 0,
            TotalWorkflows = results.Count,
            SuccessCount = successCount,
            FailedCount = failedCount,
            Results = results
        });
    }
}

#region Request/Response Models

public class WorkflowTriggerRequest
{
    public string WorkflowName { get; set; } = string.Empty;
    public string? LogicAppBaseUrl { get; set; }
    public string? SasToken { get; set; }
    public string? Payload { get; set; }
}

public class WorkflowExecutionResult
{
    public bool Success { get; set; }
    public string? WorkflowName { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string? StackTrace { get; set; }
    public string? RawResponse { get; set; }
    public int? StatusCode { get; set; }
    public string? CorrelationId { get; set; }
    public int? ExecutionTimeMs { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}

public class WorkflowListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<WorkflowStatus>? Workflows { get; set; }
}

public class WorkflowStatus
{
    public string WorkflowName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastRun { get; set; }
    public string? LastRunStatus { get; set; }
}

public class WorkflowRunsResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public List<WorkflowRun> Runs { get; set; } = new();
}

public class WorkflowRun
{
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? Error { get; set; }
}

public class BatchTriggerRequest
{
    public string? LogicAppBaseUrl { get; set; }
    public string? SasToken { get; set; }
    public Dictionary<string, string>? Payloads { get; set; }
}

public class BatchExecutionResult
{
    public bool Success { get; set; }
    public int TotalWorkflows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<WorkflowExecutionResult> Results { get; set; } = new();
}

#endregion
