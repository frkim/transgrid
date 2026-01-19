using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace Transgrid.MockServer.Controllers;

/// <summary>
/// Controller for proxying requests to Azure Functions for debugging purposes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FunctionDebugController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FunctionDebugController> _logger;

    public FunctionDebugController(
        IHttpClientFactory httpClientFactory,
        ILogger<FunctionDebugController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes a function by proxying the request to the specified function URL.
    /// </summary>
    [HttpPost("execute")]
    public async Task<ActionResult<FunctionExecutionResult>> ExecuteFunction([FromBody] FunctionExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FunctionUrl))
        {
            return BadRequest(new FunctionExecutionResult
            {
                Success = false,
                Error = "Function URL is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest(new FunctionExecutionResult
            {
                Success = false,
                Error = "Input is required"
            });
        }

        _logger.LogInformation("Executing function at URL: {FunctionUrl}", request.FunctionUrl);

        try
        {
            // Validate URL format
            if (!Uri.TryCreate(request.FunctionUrl, UriKind.Absolute, out var uri))
            {
                return BadRequest(new FunctionExecutionResult
                {
                    Success = false,
                    Error = $"Invalid function URL format: {request.FunctionUrl}"
                });
            }

            // Validate JSON input
            try
            {
                JsonDocument.Parse(request.Input);
            }
            catch (JsonException ex)
            {
                return BadRequest(new FunctionExecutionResult
                {
                    Success = false,
                    Error = $"Invalid JSON input: {ex.Message}"
                });
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            // Build URL with function key if provided
            var requestUri = uri.ToString();
            if (!string.IsNullOrWhiteSpace(request.FunctionKey))
            {
                var separator = requestUri.Contains('?') ? '&' : '?';
                requestUri = $"{requestUri}{separator}code={Uri.EscapeDataString(request.FunctionKey)}";
            }

            var content = new StringContent(request.Input, Encoding.UTF8, "application/json");
            
            // Add correlation ID for tracing
            var correlationId = Guid.NewGuid().ToString();
            content.Headers.Add("X-Correlation-Id", correlationId);

            var response = await client.PostAsync(requestUri, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Function executed successfully. CorrelationId: {CorrelationId}, StatusCode: {StatusCode}",
                    correlationId, response.StatusCode);

                return Ok(new FunctionExecutionResult
                {
                    Success = true,
                    Output = responseBody,
                    StatusCode = (int)response.StatusCode,
                    Headers = response.Headers
                        .Concat(response.Content.Headers)
                        .ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
                });
            }
            else
            {
                _logger.LogWarning(
                    "Function returned error. CorrelationId: {CorrelationId}, StatusCode: {StatusCode}",
                    correlationId, response.StatusCode);

                // Try to parse error response
                string errorMessage;
                string? stackTrace = null;

                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("error", out var errorProp))
                    {
                        errorMessage = errorProp.GetString() ?? "Unknown error";
                    }
                    else if (root.TryGetProperty("message", out var messageProp))
                    {
                        errorMessage = messageProp.GetString() ?? "Unknown error";
                    }
                    else
                    {
                        errorMessage = responseBody;
                    }

                    if (root.TryGetProperty("stackTrace", out var stackTraceProp))
                    {
                        stackTrace = stackTraceProp.GetString();
                    }
                }
                catch
                {
                    errorMessage = responseBody;
                }

                return Ok(new FunctionExecutionResult
                {
                    Success = false,
                    Error = $"Function returned {(int)response.StatusCode}: {errorMessage}",
                    StackTrace = stackTrace,
                    StatusCode = (int)response.StatusCode
                });
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to function failed");
            return Ok(new FunctionExecutionResult
            {
                Success = false,
                Error = $"Failed to connect to function: {ex.Message}",
                StackTrace = ex.StackTrace
            });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Function request timed out");
            return Ok(new FunctionExecutionResult
            {
                Success = false,
                Error = "Function request timed out after 30 seconds",
                StackTrace = ex.StackTrace
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing function");
            return Ok(new FunctionExecutionResult
            {
                Success = false,
                Error = $"Unexpected error: {ex.Message}",
                StackTrace = ex.StackTrace
            });
        }
    }
}

/// <summary>
/// Request model for function execution.
/// </summary>
public class FunctionExecutionRequest
{
    public string FunctionUrl { get; set; } = string.Empty;
    public string? FunctionKey { get; set; }
    public string Input { get; set; } = string.Empty;
}

/// <summary>
/// Result model for function execution.
/// </summary>
public class FunctionExecutionResult
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public string? StackTrace { get; set; }
    public int? StatusCode { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}
