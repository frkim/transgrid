using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace Transgrid.MockServer.Pages.RneExport;

public class IndexModel : PageModel
{
    private readonly IConfiguration _configuration;
    
    public IndexModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public string BaseApiUrl => $"{Request.Scheme}://{Request.Host}";
    
    /// <summary>
    /// Gets the Logic App HTTP Trigger URL from configuration.
    /// Falls back to a template URL if not configured.
    /// </summary>
    public string LogicAppHttpTriggerUrl => _configuration["LogicApp:HttpTriggerUrl"] 
        ?? "https://{logic-app-name}.azurewebsites.net/api/rne-http-trigger/triggers/Manual_HTTP_Trigger/invoke?api-version=2022-05-01";
    
    public void OnGet()
    {
    }
}
