using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Transgrid.MockServer.Pages.FunctionDebug;

public class IndexModel : PageModel
{
    private readonly IConfiguration _configuration;

    public IndexModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string FunctionBaseUrl { get; private set; } = string.Empty;
    public string FunctionKey { get; private set; } = string.Empty;
    
    // Function endpoints
    public string TransformTrainPlanUrl { get; private set; } = string.Empty;
    public string TransformNegotiatedRatesUrl { get; private set; } = string.Empty;
    public string ProcessCifOnDemandUrl { get; private set; } = string.Empty;
    public string TransformCifScheduleUrl { get; private set; } = string.Empty;
    
    // GitHub source links
    public const string GitHubRepoBase = "https://github.com/frkim/transgrid/tree/main/sources/functions/Transgrid.Functions/Functions";
    public const string TransformTrainPlanSource = $"{GitHubRepoBase}/TransformTrainPlan.cs";
    public const string TransformNegotiatedRatesSource = $"{GitHubRepoBase}/TransformNegotiatedRates.cs";
    public const string ProcessCifFileSource = $"{GitHubRepoBase}/ProcessCifFile.cs";

    public void OnGet()
    {
        // Read from configuration (environment variables take precedence over appsettings)
        FunctionBaseUrl = _configuration["FunctionDebug:FunctionBaseUrl"] 
                      ?? _configuration["FUNCTION_URL"] 
                      ?? "https://func-transgrid-transform-dev.azurewebsites.net";
        
        // Remove trailing slash if present
        FunctionBaseUrl = FunctionBaseUrl.TrimEnd('/');
        
        FunctionKey = _configuration["FunctionDebug:FunctionKey"] 
                      ?? _configuration["FUNCTION_KEY"] 
                      ?? string.Empty;
        
        // Build function URLs
        TransformTrainPlanUrl = $"{FunctionBaseUrl}/api/TransformTrainPlan";
        TransformNegotiatedRatesUrl = $"{FunctionBaseUrl}/api/TransformNegotiatedRates";
        ProcessCifOnDemandUrl = $"{FunctionBaseUrl}/api/ProcessCifOnDemand";
        TransformCifScheduleUrl = $"{FunctionBaseUrl}/api/TransformCifSchedule";
    }
}
