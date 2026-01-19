using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Transgrid.MockServer.Pages.Documentation;

public class IndexModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IWebHostEnvironment environment, ILogger<IndexModel> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public string UseCasesMarkdown { get; private set; } = string.Empty;
    public string UseCaseDetailsMarkdown { get; private set; } = string.Empty;

    public void OnGet()
    {
        // Load the markdown files from the documents folder
        var documentsPath = Path.Combine(_environment.ContentRootPath, "wwwroot", "documents");
        
        var useCasesPath = Path.Combine(documentsPath, "Azure Integration Services Use Cases.md");
        var detailsPath = Path.Combine(documentsPath, "Use Case Details.md");

        if (System.IO.File.Exists(useCasesPath))
        {
            UseCasesMarkdown = System.IO.File.ReadAllText(useCasesPath);
            _logger.LogDebug("Loaded Use Cases markdown: {Length} chars", UseCasesMarkdown.Length);
        }
        else
        {
            _logger.LogWarning("Use Cases markdown file not found at: {Path}", useCasesPath);
            UseCasesMarkdown = "# Use Cases\n\nContent not available.";
        }

        if (System.IO.File.Exists(detailsPath))
        {
            UseCaseDetailsMarkdown = System.IO.File.ReadAllText(detailsPath);
            _logger.LogDebug("Loaded Use Case Details markdown: {Length} chars", UseCaseDetailsMarkdown.Length);
        }
        else
        {
            _logger.LogWarning("Use Case Details markdown file not found at: {Path}", detailsPath);
            UseCaseDetailsMarkdown = "# Use Case Details\n\nContent not available.";
        }
    }
}
