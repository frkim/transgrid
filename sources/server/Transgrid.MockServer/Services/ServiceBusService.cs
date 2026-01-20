using Azure.Messaging.ServiceBus;
using System.Text.Json;

namespace Transgrid.MockServer.Services;

/// <summary>
/// Service for publishing messages to Azure Service Bus
/// </summary>
public class ServiceBusService : IAsyncDisposable
{
    private readonly ILogger<ServiceBusService> _logger;
    private readonly IConfiguration _configuration;
    private ServiceBusClient? _client;
    private ServiceBusSender? _sender;
    private bool _isConfigured;

    public ServiceBusService(ILogger<ServiceBusService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        InitializeClient();
    }

    private void InitializeClient()
    {
        var connectionString = _configuration["ServiceBus:ConnectionString"];
        var queueName = _configuration["ServiceBus:QueueName"];

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(queueName))
        {
            _logger.LogWarning("Service Bus configuration not found. Messages will be simulated locally.");
            _isConfigured = false;
            return;
        }

        try
        {
            _client = new ServiceBusClient(connectionString);
            _sender = _client.CreateSender(queueName);
            _isConfigured = true;
            _logger.LogInformation("Service Bus client initialized for queue: {QueueName}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Service Bus client");
            _isConfigured = false;
        }
    }

    /// <summary>
    /// Check if Service Bus is configured and available
    /// </summary>
    public bool IsConfigured => _isConfigured;

    /// <summary>
    /// Publish a Platform Event to Service Bus
    /// </summary>
    public async Task<MessagePublishResult> PublishPlatformEventAsync(string eventType, IEnumerable<string> negotiatedRateIds)
    {
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;

        var messageData = new
        {
            eventType,
            negotiatedRateIds = negotiatedRateIds.ToArray(),
            timestamp = timestamp.ToString("O"),
            correlationId
        };

        var result = new MessagePublishResult
        {
            CorrelationId = correlationId,
            Timestamp = timestamp,
            EventType = eventType,
            RecordCount = negotiatedRateIds.Count()
        };

        if (!_isConfigured || _sender == null)
        {
            _logger.LogInformation("Service Bus not configured. Simulating local message: {EventType} with {Count} records", 
                eventType, negotiatedRateIds.Count());
            result.IsSimulated = true;
            result.Success = true;
            return result;
        }

        try
        {
            var jsonPayload = JsonSerializer.Serialize(messageData);
            var message = new ServiceBusMessage(jsonPayload)
            {
                ContentType = "application/json",
                CorrelationId = correlationId,
                Subject = eventType,
                MessageId = Guid.NewGuid().ToString()
            };
            message.ApplicationProperties["EventType"] = eventType;
            message.ApplicationProperties["RecordCount"] = negotiatedRateIds.Count();

            await _sender.SendMessageAsync(message);
            _logger.LogInformation("Published message to Service Bus: {EventType} with {Count} records, CorrelationId: {CorrelationId}", 
                eventType, negotiatedRateIds.Count(), correlationId);

            result.Success = true;
            result.IsSimulated = false;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to Service Bus");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender != null)
        {
            await _sender.DisposeAsync();
        }
        if (_client != null)
        {
            await _client.DisposeAsync();
        }
    }
}

/// <summary>
/// Result of publishing a message to Service Bus
/// </summary>
public class MessagePublishResult
{
    public bool Success { get; set; }
    public bool IsSimulated { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public string? ErrorMessage { get; set; }
}
