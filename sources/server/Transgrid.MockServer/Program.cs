using Transgrid.MockServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSingleton<DataStore>();

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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
