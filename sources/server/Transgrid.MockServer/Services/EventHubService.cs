using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using System.Text.Json;

namespace Transgrid.MockServer.Services;

/// <summary>
/// Service for publishing events to Azure Event Hub
/// </summary>
public class EventHubService : IAsyncDisposable
{
    private readonly ILogger<EventHubService> _logger;
    private readonly IConfiguration _configuration;
    private EventHubProducerClient? _producerClient;
    private bool _isConfigured;

    public EventHubService(ILogger<EventHubService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        InitializeClient();
    }

    private void InitializeClient()
    {
        var connectionString = _configuration["EventHub:ConnectionString"];
        var eventHubName = _configuration["EventHub:Name"];

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(eventHubName))
        {
            _logger.LogWarning("Event Hub configuration not found. Events will be simulated locally.");
            _isConfigured = false;
            return;
        }

        try
        {
            _producerClient = new EventHubProducerClient(connectionString, eventHubName);
            _isConfigured = true;
            _logger.LogInformation("Event Hub client initialized for hub: {HubName}", eventHubName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Event Hub client");
            _isConfigured = false;
        }
    }

    /// <summary>
    /// Check if Event Hub is configured and available
    /// </summary>
    public bool IsConfigured => _isConfigured;

    /// <summary>
    /// Publish a Platform Event to Event Hub
    /// </summary>
    public async Task<EventPublishResult> PublishPlatformEventAsync(string eventType, IEnumerable<string> negotiatedRateIds)
    {
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;

        var eventData = new
        {
            eventType,
            negotiatedRateIds = negotiatedRateIds.ToArray(),
            timestamp = timestamp.ToString("O"),
            correlationId
        };

        var result = new EventPublishResult
        {
            CorrelationId = correlationId,
            Timestamp = timestamp,
            EventType = eventType,
            RecordCount = negotiatedRateIds.Count()
        };

        if (!_isConfigured || _producerClient == null)
        {
            _logger.LogInformation("Event Hub not configured. Simulating local event: {EventType} with {Count} records", 
                eventType, negotiatedRateIds.Count());
            result.IsSimulated = true;
            result.Success = true;
            return result;
        }

        try
        {
            using EventDataBatch eventBatch = await _producerClient.CreateBatchAsync();
            var jsonPayload = JsonSerializer.Serialize(eventData);
            var eventMessage = new EventData(jsonPayload)
            {
                ContentType = "application/json",
                CorrelationId = correlationId
            };
            eventMessage.Properties["EventType"] = eventType;
            eventMessage.Properties["RecordCount"] = negotiatedRateIds.Count();

            if (!eventBatch.TryAdd(eventMessage))
            {
                _logger.LogError("Event is too large for the batch");
                result.Success = false;
                result.ErrorMessage = "Event too large for batch";
                return result;
            }

            await _producerClient.SendAsync(eventBatch);
            _logger.LogInformation("Published event to Event Hub: {EventType} with {Count} records, CorrelationId: {CorrelationId}", 
                eventType, negotiatedRateIds.Count(), correlationId);

            result.Success = true;
            result.IsSimulated = false;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event to Event Hub");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_producerClient != null)
        {
            await _producerClient.DisposeAsync();
        }
    }
}

/// <summary>
/// Result of publishing an event to Event Hub
/// </summary>
public class EventPublishResult
{
    public bool Success { get; set; }
    public bool IsSimulated { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public string? ErrorMessage { get; set; }
}
