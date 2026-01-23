using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Transgrid.MockServer.Pages.WorkflowDebug;

public class IndexModel : PageModel
{
    private readonly IConfiguration _configuration;

    public IndexModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string LogicAppBaseUrl { get; private set; } = string.Empty;
    
    /// <summary>
    /// List of available workflows with their details
    /// </summary>
    public List<WorkflowInfo> Workflows { get; } = new()
    {
        new WorkflowInfo
        {
            Id = "rne-daily-export",
            Name = "RNE Daily Export",
            Description = "Daily export of train plans from OpsAPI to RNE SFTP servers. Runs at 6:00 AM (Romance Standard Time).",
            TriggerType = "Recurrence",
            WorkflowType = "DailyExport",
            UseCase = "UC1",
            SamplePayload = null, // Recurrence trigger, no payload
            HasHttpTrigger = false
        },
        new WorkflowInfo
        {
            Id = "rne-d2-export",
            Name = "RNE D+2 Export",
            Description = "D+2 export of train plans (2 days ahead). Runs at 6:30 AM (Romance Standard Time).",
            TriggerType = "Recurrence",
            WorkflowType = "D2Export",
            UseCase = "UC1",
            SamplePayload = null,
            HasHttpTrigger = false
        },
        new WorkflowInfo
        {
            Id = "rne-http-trigger",
            Name = "RNE HTTP Trigger",
            Description = "On-demand HTTP trigger to export train plans for a specific date.",
            TriggerType = "HTTP Request",
            WorkflowType = "HttpTriggerExport",
            UseCase = "UC1",
            SamplePayload = """
            {
              "travelDate": "2026-01-25",
              "exportType": "daily",
              "trainIds": []
            }
            """,
            HasHttpTrigger = true
        },
        new WorkflowInfo
        {
            Id = "rne-retry-failed",
            Name = "RNE Retry Failed",
            Description = "Retries failed train plan exports from the FailedExports table. Runs at 7:00 AM.",
            TriggerType = "Recurrence",
            WorkflowType = "RetryFailed",
            UseCase = "UC1",
            SamplePayload = null,
            HasHttpTrigger = false
        },
        new WorkflowInfo
        {
            Id = "sf-negotiated-rates",
            Name = "SF Negotiated Rates",
            Description = "Processes negotiated rates from Salesforce Event Hub and publishes to distribution channels.",
            TriggerType = "Event Hub",
            WorkflowType = "NegotiatedRates",
            UseCase = "UC2",
            SamplePayload = null,
            HasHttpTrigger = false
        },
        new WorkflowInfo
        {
            Id = "nr-cif-processing",
            Name = "NR CIF Processing",
            Description = "On-demand HTTP trigger to process Network Rail CIF files.",
            TriggerType = "HTTP Request",
            WorkflowType = "CifProcessing",
            UseCase = "UC3",
            SamplePayload = """
            {
              "fileType": "update",
              "forceRefresh": false
            }
            """,
            HasHttpTrigger = true
        }
    };

    public void OnGet()
    {
        // Read Logic App base URL from configuration
        LogicAppBaseUrl = _configuration["WorkflowDebug:LogicAppBaseUrl"] 
                          ?? _configuration["LOGIC_APP_URL"] 
                          ?? "https://logic-transgrid-rne-export-dev.azurewebsites.net";
        
        LogicAppBaseUrl = LogicAppBaseUrl.TrimEnd('/');
    }
}

/// <summary>
/// Information about a workflow
/// </summary>
public class WorkflowInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public string UseCase { get; set; } = string.Empty;
    public string? SamplePayload { get; set; }
    public bool HasHttpTrigger { get; set; }
}
