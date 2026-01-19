using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Transgrid.MockServer.Pages.FunctionDebug;

public class IndexModel : PageModel
{
    private readonly IConfiguration _configuration;

    public IndexModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string FunctionUrl { get; private set; } = string.Empty;
    public string FunctionKey { get; private set; } = string.Empty;

    public void OnGet()
    {
        // Read from configuration (environment variables take precedence over appsettings)
        FunctionUrl = _configuration["FunctionDebug:FunctionUrl"] 
                      ?? _configuration["FUNCTION_URL"] 
                      ?? "https://func-transgrid-transform-dev.azurewebsites.net/api/TransformTrainPlan";
        
        FunctionKey = _configuration["FunctionDebug:FunctionKey"] 
                      ?? _configuration["FUNCTION_KEY"] 
                      ?? string.Empty;
    }
}
