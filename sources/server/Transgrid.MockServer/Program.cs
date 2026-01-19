using Transgrid.MockServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSingleton<DataStore>();
builder.Services.AddHttpClient(); // For FunctionDebug controller

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Transgrid Mock Server API",
        Version = "v1",
        Description = "Mock server API for Starline International train operations - simulating OpsAPI, Salesforce, and Network Rail endpoints",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Transgrid Project",
            Url = new Uri("https://github.com/frkim/transgrid")
        }
    });
});

// Add CORS for API access
builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        // In development, allow any origin for ease of testing
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    }
    else
    {
        // In production, use a restrictive policy
        // Customize with specific origins as needed for your deployment
        options.AddDefaultPolicy(policy =>
        {
            // No origins allowed by default - configure as needed
            policy.WithMethods("GET", "POST", "PUT", "DELETE")
                  .WithHeaders("Content-Type");
        });
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Transgrid Mock Server API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Transgrid Mock Server API Documentation";
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
