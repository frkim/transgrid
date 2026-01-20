using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Transgrid.Functions.Models;
using Transgrid.Functions.Services;

namespace Transgrid.Functions;

/// <summary>
/// Azure Function for transforming Salesforce negotiated rate data to CSV format.
/// Implements the three extract routes: IDL/S3, GDS Air, and BeNe.
/// Called by Logic Apps workflows to generate CSV files for blob storage.
/// </summary>
public class TransformNegotiatedRates
{
    private readonly ILogger<TransformNegotiatedRates> _logger;
    private readonly ICsvGeneratorService _csvGeneratorService;

    public TransformNegotiatedRates(
        ILogger<TransformNegotiatedRates> logger,
        ICsvGeneratorService csvGeneratorService)
    {
        _logger = logger;
        _csvGeneratorService = csvGeneratorService;
    }

    /// <summary>
    /// Transforms negotiated rate data to CSV format for all three routes.
    /// </summary>
    [Function("TransformNegotiatedRates")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var correlationId = GetCorrelationId(req);
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        _logger.LogInformation("TransformNegotiatedRates function started. CorrelationId: {CorrelationId}", correlationId);

        try
        {
            // 1. Parse input JSON
            var requestBody = await req.ReadAsStringAsync();
            
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                _logger.LogWarning("Empty request body received");
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                    "Request body is empty. Expected negotiated rates extract request.");
            }

            NegotiatedRateExtractRequest? extractRequest;
            try
            {
                extractRequest = JsonSerializer.Deserialize<NegotiatedRateExtractRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON request body");
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                    $"Invalid JSON format: {ex.Message}");
            }

            if (extractRequest == null || extractRequest.NegotiatedRates.Count == 0)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                    "No negotiated rates provided in request");
            }

            extractRequest.CorrelationId = correlationId;

            _logger.LogInformation(
                "Processing {Count} negotiated rates for route {Route}. CorrelationId: {CorrelationId}",
                extractRequest.NegotiatedRates.Count, extractRequest.ExtractRoute, correlationId);

            // 2. Apply priority filter
            var rates = extractRequest.NegotiatedRates.AsEnumerable();
            if (extractRequest.Priority != "ALL")
            {
                rates = rates.Where(r => r.Priority.Equals(extractRequest.Priority, StringComparison.OrdinalIgnoreCase));
            }

            var ratesList = rates.ToList();
            var response = new NegotiatedRateExtractResponse
            {
                CorrelationId = correlationId,
                TotalRecords = ratesList.Count
            };

            // 3. Process routes
            var routesToProcess = extractRequest.ExtractRoute == "ALL"
                ? new[] { "IDL_S3", "GDS_AIR", "BENE" }
                : new[] { extractRequest.ExtractRoute };

            foreach (var route in routesToProcess)
            {
                var result = ProcessRoute(route, ratesList);
                response.Routes.Add(result);
                
                if (result.Success)
                {
                    response.SuccessCount += result.RecordCount;
                }
                else
                {
                    response.FailedCount += result.RecordCount;
                }
            }

            response.Success = response.FailedCount == 0;

            _logger.LogInformation(
                "Completed processing. Total: {Total}, Success: {Success}, Failed: {Failed}",
                response.TotalRecords, response.SuccessCount, response.FailedCount);

            // 4. Return response
            var httpResponse = req.CreateResponse(HttpStatusCode.OK);
            httpResponse.Headers.Add("X-Correlation-Id", correlationId);
            await httpResponse.WriteAsJsonAsync(response);
            
            return httpResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in TransformNegotiatedRates function");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                $"Unexpected error: {ex.Message}");
        }
    }

    private RouteExtractResult ProcessRoute(string route, List<NegotiatedRateInput> rates)
    {
        var result = new RouteExtractResult
        {
            RouteCode = route,
            FileName = $"NegotiatedRates_{route}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv"
        };

        try
        {
            List<NegotiatedRateInput> filteredRates;
            string csvContent;

            switch (route.ToUpperInvariant())
            {
                case "IDL_S3":
                    result.RouteName = "IDL/S3 (Internal Distribution)";
                    filteredRates = _csvGeneratorService.FilterForIdlS3(rates).ToList();
                    csvContent = _csvGeneratorService.GenerateIdlS3Csv(filteredRates);
                    break;

                case "GDS_AIR":
                    result.RouteName = "GDS Air (Travel Agents)";
                    filteredRates = _csvGeneratorService.FilterForGdsAir(rates).ToList();
                    csvContent = _csvGeneratorService.GenerateGdsAirCsv(filteredRates);
                    break;

                case "BENE":
                    result.RouteName = "BeNe (External Partners)";
                    filteredRates = _csvGeneratorService.FilterForBeNe(rates).ToList();
                    csvContent = _csvGeneratorService.GenerateBeneCsv(filteredRates);
                    break;

                default:
                    result.Success = false;
                    result.ErrorMessage = $"Unknown route: {route}";
                    return result;
            }

            result.RecordCount = filteredRates.Count;
            result.CsvContent = csvContent;
            result.Success = true;

            _logger.LogInformation("Generated CSV for route {Route} with {Count} records", route, result.RecordCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing route {Route}", route);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
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
        
        var error = new
        {
            error = message,
            statusCode = (int)statusCode,
            timestamp = DateTime.UtcNow
        };
        
        await response.WriteAsJsonAsync(error);
        return response;
    }
}
