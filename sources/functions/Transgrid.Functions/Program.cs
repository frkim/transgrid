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
        
        // Register services
        services.AddSingleton<IXmlTransformService, XmlTransformService>();
        services.AddSingleton<IXmlValidationService, XmlValidationService>();
        services.AddSingleton<IReferenceDataService, ReferenceDataService>();
    })
    .Build();

host.Run();
