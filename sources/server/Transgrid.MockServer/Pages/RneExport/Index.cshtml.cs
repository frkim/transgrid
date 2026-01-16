using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Transgrid.MockServer.Pages.RneExport;

public class IndexModel : PageModel
{
    public string BaseApiUrl => $"{Request.Scheme}://{Request.Host}";
    
    public void OnGet()
    {
    }
}
