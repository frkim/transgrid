using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Transgrid.Functions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Application Insights for distributed tracing
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        // Register services for RNE Export (Use Case 1)
        services.AddSingleton<IXmlTransformService, XmlTransformService>();
        services.AddSingleton<IXmlValidationService, XmlValidationService>();
        services.AddSingleton<IReferenceDataService, ReferenceDataService>();
        
        // Register services for Salesforce Negotiated Rates (Use Case 2)
        services.AddSingleton<ICsvGeneratorService, CsvGeneratorService>();
    })
    .Build();

host.Run();
