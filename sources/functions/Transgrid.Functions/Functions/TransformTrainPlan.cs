using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Transgrid.Functions.Models;
using Transgrid.Functions.Services;

namespace Transgrid.Functions;

/// <summary>
/// Azure Function for transforming train plan JSON to TAF-JSG XML format.
/// Called by Logic Apps workflows to convert data before uploading to SFTP.
/// </summary>
public class TransformTrainPlan
{
    private readonly ILogger<TransformTrainPlan> _logger;
    private readonly IXmlTransformService _xmlTransformService;
    private readonly IXmlValidationService _xmlValidationService;
    private readonly IReferenceDataService _referenceDataService;

    public TransformTrainPlan(
        ILogger<TransformTrainPlan> logger,
        IXmlTransformService xmlTransformService,
        IXmlValidationService xmlValidationService,
        IReferenceDataService referenceDataService)
    {
        _logger = logger;
        _xmlTransformService = xmlTransformService;
        _xmlValidationService = xmlValidationService;
        _referenceDataService = referenceDataService;
    }

    /// <summary>
    /// Transforms a train plan JSON object to TAF-JSG XML format.
    /// </summary>
    /// <param name="req">HTTP request containing train plan JSON</param>
    /// <returns>XML string or error response</returns>
    [Function("TransformTrainPlan")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var correlationId = GetCorrelationId(req);
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });

        _logger.LogInformation("TransformTrainPlan function started. CorrelationId: {CorrelationId}", correlationId);

        try
        {
            // 1. Parse input JSON
            var requestBody = await req.ReadAsStringAsync();
            
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                _logger.LogWarning("Empty request body received");
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                    "Request body is empty. Expected train plan JSON.");
            }

            TrainPlanInput? trainPlan;
            try
            {
                trainPlan = JsonSerializer.Deserialize<TrainPlanInput>(requestBody, new JsonSerializerOptions
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

            if (trainPlan == null)
            {
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                    "Failed to deserialize train plan");
            }

            // 2. Validate required fields
            var validationErrors = ValidateInput(trainPlan);
            if (validationErrors.Any())
            {
                _logger.LogWarning("Input validation failed: {Errors}", string.Join(", ", validationErrors));
                return await CreateErrorResponse(req, HttpStatusCode.BadRequest, 
                    $"Validation failed: {string.Join("; ", validationErrors)}");
            }

            _logger.LogInformation(
                "Processing train plan. ServiceCode: {ServiceCode}, TravelDate: {TravelDate}, Origin: {Origin}, Destination: {Destination}",
                trainPlan.ServiceCode, trainPlan.TravelDate, trainPlan.Origin, trainPlan.Destination);

            // 3. Load reference data
            var referenceData = await _referenceDataService.GetReferenceDataAsync();

            // 4. Transform to XML
            string xml;
            try
            {
                xml = _xmlTransformService.Transform(trainPlan, referenceData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "XML transformation failed for train {ServiceCode}", trainPlan.ServiceCode);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                    $"XML transformation failed: {ex.Message}");
            }

            // 5. Validate XML
            var xmlValidation = _xmlValidationService.Validate(xml);
            if (!xmlValidation.IsValid)
            {
                var errorMessages = string.Join("; ", xmlValidation.Errors.Select(e => e.Message));
                _logger.LogWarning("XML validation failed for train {ServiceCode}: {Errors}", 
                    trainPlan.ServiceCode, errorMessages);
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                    $"XML validation failed: {errorMessages}");
            }

            _logger.LogInformation(
                "Successfully transformed train plan to XML. ServiceCode: {ServiceCode}, XmlLength: {Length}",
                trainPlan.ServiceCode, xml.Length);

            // 6. Return XML response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
            response.Headers.Add("X-Correlation-Id", correlationId);
            response.Headers.Add("X-Train-ServiceCode", trainPlan.ServiceCode);
            response.Headers.Add("X-Travel-Date", trainPlan.TravelDate);
            await response.WriteStringAsync(xml);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in TransformTrainPlan function");
            return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                $"Unexpected error: {ex.Message}");
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

    private static List<string> ValidateInput(TrainPlanInput trainPlan)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(trainPlan.ServiceCode))
        {
            errors.Add("ServiceCode is required");
        }

        if (string.IsNullOrWhiteSpace(trainPlan.TravelDate))
        {
            errors.Add("TravelDate is required");
        }
        else if (!DateTime.TryParse(trainPlan.TravelDate, out _))
        {
            errors.Add("TravelDate must be a valid date");
        }

        if (string.IsNullOrWhiteSpace(trainPlan.Origin))
        {
            errors.Add("Origin is required");
        }

        if (string.IsNullOrWhiteSpace(trainPlan.Destination))
        {
            errors.Add("Destination is required");
        }

        if (trainPlan.Origin == trainPlan.Destination && !string.IsNullOrWhiteSpace(trainPlan.Origin))
        {
            errors.Add("Origin and Destination must be different");
        }

        return errors;
    }

    private static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData req, HttpStatusCode statusCode, string message)
    {
        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json");
        
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
