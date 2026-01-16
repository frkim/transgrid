---
applyTo: '**/*.cs'
---
<!-- markdownlint-disable MD024 -->

# .NET Development Best Practices and Guidelines

## 1. Code Organization and Structure

### Project Structure

- Follow **clean architecture** principles with clear separation of concerns
- Organize code into logical layers: API/Presentation, Application/Business Logic, Domain, Infrastructure
- Keep **Controllers thin** – delegate business logic to services
- Use **feature folders** for better organization when appropriate
- Place shared/common code in separate projects or folders

### Naming Conventions

- **PascalCase**: Classes, methods, properties, public fields, namespaces
- **camelCase**: Private fields, local variables, parameters
- **Prefix private fields** with underscore: `_fieldName`
- Use **meaningful, descriptive names** – avoid abbreviations unless widely understood
- Interface names start with `I`: `IUserService`, `IRepository<T>`
- Async methods end with `Async`: `GetUserAsync`, `SaveDataAsync`
- Test methods: `MethodName_Scenario_ExpectedBehavior`

## 2. Modern C# Features

### Use Latest Language Features

- Prefer **record types** for DTOs and immutable data models
- Use **primary constructors** (C# 12+) for simple dependency injection
- Leverage **init-only properties** for immutable objects
- Use **nullable reference types** and enable them in project
- Apply **pattern matching** for cleaner conditional logic
- Use **switch expressions** instead of switch statements where appropriate
- Prefer **collection expressions** (C# 12+): `[1, 2, 3]` over `new[] { 1, 2, 3 }`

### Example

```csharp
// Good: Modern C# with records and nullable reference types
public record UserDto(string Name, string Email, int? Age);

// Good: Primary constructor (C# 12+)
public class UserService(IUserRepository repository, ILogger<UserService> logger)
{
    public async Task<User?> GetUserAsync(int id)
    {
        return await repository.FindByIdAsync(id);
    }
}
```

## 3. Asynchronous Programming

### Async/Await Best Practices

- Always use `async`/`await` for I/O-bound operations (database, file, network)
- **Do NOT use `.Result` or `.Wait()`** – it can cause deadlocks
- Use `ConfigureAwait(false)` in library code to avoid context capturing
- Return `Task` or `Task<T>`, never `async void` (except event handlers)
- Use `ValueTask<T>` for high-performance scenarios when result is often synchronous
- Prefer `IAsyncEnumerable<T>` for streaming large datasets

### Example

```csharp
// Good: Proper async pattern
public async Task<IEnumerable<User>> GetUsersAsync(CancellationToken cancellationToken = default)
{
    return await _repository.GetAllAsync(cancellationToken);
}

// Good: Streaming with IAsyncEnumerable
public async IAsyncEnumerable<User> StreamUsersAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var user in _repository.StreamAllAsync(cancellationToken))
    {
        yield return user;
    }
}
```

## 4. Dependency Injection

### DI Best Practices

- Register services in `Program.cs` or use extension methods: `services.AddMyServices()`
- Use **constructor injection** (not property or method injection)
- Inject **interfaces, not concrete types**
- Be mindful of service lifetimes:
  - **Transient**: Created each time requested (stateless services)
  - **Scoped**: Created once per request/scope (DbContext, request-specific services)
  - **Singleton**: Created once for application lifetime (caches, configuration)
- Avoid **service locator pattern** – don't inject `IServiceProvider`
- Use **keyed services** (.NET 8+) when multiple implementations needed

### Example

```csharp
// Good: Clean DI registration
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUserServices(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();
        services.AddTransient<IUserValidator, UserValidator>();
        return services;
    }
}

// Usage in Program.cs
builder.Services.AddUserServices();
```

## 5. Error Handling and Validation

### Exception Handling

- Use **specific exception types** – avoid generic `Exception`
- Create **custom exceptions** that inherit from appropriate base classes
- Handle exceptions at the **appropriate level** – don't swallow them unnecessarily
- Use **global exception handlers** in ASP.NET Core via middleware
- Log exceptions with **structured logging** including context
- Use **Result pattern** or `OneOf<T>` for expected failures instead of exceptions

### Validation

- Validate at **API boundaries** – controller actions, message handlers
- Use **FluentValidation** for complex validation logic
- Apply **data annotations** for simple model validation
- Return **validation errors in a consistent format** (ProblemDetails)
- Validate **business rules in domain layer**, not in infrastructure

### Example

```csharp
// Good: Custom exception
public class UserNotFoundException : Exception
{
    public UserNotFoundException(int userId) 
        : base($"User with ID {userId} was not found")
    {
        UserId = userId;
    }
    
    public int UserId { get; }
}

// Good: Global exception handler
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, 
        Exception exception, 
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred");

        var problemDetails = exception switch
        {
            UserNotFoundException ex => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User not found",
                Detail = ex.Message
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An error occurred"
            }
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        
        return true;
    }
}
```

## 6. Configuration Management

### Configuration Best Practices

- Use **strongly-typed configuration** with `IOptions<T>`
- Store sensitive data in **Azure Key Vault, User Secrets, or environment variables**
- Never commit secrets to source control
- Use **configuration validation** with `ValidateDataAnnotations()` or `ValidateOnStart()`
- Organize settings into logical sections
- Use **different configurations per environment** (Development, Staging, Production)

### Example

```csharp
// Good: Strongly-typed configuration
public class DatabaseOptions
{
    public const string SectionName = "Database";
    
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
    
    [Range(1, 100)]
    public int MaxRetryAttempts { get; set; } = 3;
}

// Registration in Program.cs
builder.Services.AddOptions<DatabaseOptions>()
    .BindConfiguration(DatabaseOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

## 7. Database and EF Core

### EF Core Best Practices

- Use **async methods** for all database operations
- Apply **eager loading** (`.Include()`) judiciously – avoid N+1 queries
- Use **projection** (`.Select()`) to fetch only needed data
- Enable **query splitting** for complex includes
- Use **compiled queries** for frequently executed queries
- Implement **database migrations** for schema changes
- Use **repository pattern** or **specification pattern** for complex queries
- Always use **parameterized queries** – never string concatenation
- Configure **connection resiliency** for transient failures
- Use **batching** for bulk operations

### Example

```csharp
// Good: Optimized EF Core query
public async Task<UserDto[]> GetUsersWithOrdersAsync(CancellationToken cancellationToken)
{
    return await _dbContext.Users
        .AsNoTracking()
        .Include(u => u.Orders.Take(10))
        .AsSplitQuery()
        .Select(u => new UserDto(u.Name, u.Email, u.Orders.Count))
        .ToArrayAsync(cancellationToken);
}
```

## 8. API Development (ASP.NET Core)

### API Best Practices

- Use **minimal APIs** for simple endpoints, **controllers** for complex ones
- Apply **API versioning** from the start
- Use **ProblemDetails** for error responses
- Implement **health checks** (`/health`, `/health/ready`)
- Apply **rate limiting** to prevent abuse
- Use **output caching** or **response caching** where appropriate
- Support **cancellation tokens** in all async endpoints
- Return appropriate **HTTP status codes** (200, 201, 204, 400, 404, 500)
- Use **OpenAPI/Swagger** for documentation
- Apply **CORS** policies carefully

### Example

```csharp
// Good: Minimal API with best practices
app.MapGet("/api/users/{id}", async (
    int id, 
    IUserService userService, 
    CancellationToken cancellationToken) =>
{
    var user = await userService.GetUserAsync(id, cancellationToken);
    return user is not null ? Results.Ok(user) : Results.NotFound();
})
.WithName("GetUser")
.WithOpenApi()
.Produces<UserDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);
```

## 9. Logging and Monitoring

### Logging Best Practices

- Use **structured logging** with log levels: Trace, Debug, Information, Warning, Error, Critical
- Use **source generators** for high-performance logging: `[LoggerMessage]`
- Include **correlation IDs** in logs for request tracing
- Log **exceptions with context**, not just messages
- Use **semantic logging** – log properties, not interpolated strings
- Configure **appropriate log levels** per environment
- Integrate with **Application Insights** or **OpenTelemetry** for distributed tracing

### Example

```csharp
// Good: High-performance logging with source generator
public partial class UserService
{
    private readonly ILogger<UserService> _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Retrieving user with ID {UserId}")]
    private partial void LogRetrievingUser(int userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "User {UserId} not found")]
    private partial void LogUserNotFound(int userId);

    public async Task<User?> GetUserAsync(int id)
    {
        LogRetrievingUser(id);
        
        var user = await _repository.FindByIdAsync(id);
        
        if (user is null)
            LogUserNotFound(id);
            
        return user;
    }
}
```

## 10. Testing

### Testing Best Practices

- Write **unit tests** for business logic
- Use **integration tests** for database and external service interactions
- Apply **AAA pattern**: Arrange, Act, Assert
- Use **xUnit, NUnit, or MSTest** as test framework
- Mock dependencies with **Moq, NSubstitute, or FakeItEasy**
- Use **WebApplicationFactory** for integration testing ASP.NET Core apps
- Use **Testcontainers** for testing with real databases
- Aim for **high code coverage** on critical paths (70-80%)
- Write **fast, isolated, repeatable tests**
- Use **BDD frameworks** (SpecFlow) for acceptance tests if needed

### Example

```csharp
// Good: Unit test example
public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockRepository;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _mockRepository = new Mock<IUserRepository>();
        _sut = new UserService(_mockRepository.Object, Mock.Of<ILogger<UserService>>());
    }

    [Fact]
    public async Task GetUserAsync_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var expectedUser = new User { Id = 1, Name = "John" };
        _mockRepository
            .Setup(r => r.FindByIdAsync(1, default))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _sut.GetUserAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedUser.Id, result.Id);
    }
}
```

## 11. Performance and Optimization

### Performance Best Practices

- Use **Span<T>** and **Memory<T>** for memory-efficient operations
- Apply **object pooling** (`ObjectPool<T>`) for frequently allocated objects
- Use **ArrayPool<T>** for temporary array allocations
- Leverage **System.Text.Json** for fast JSON serialization
- Enable **response compression** for APIs
- Use **caching** strategically (memory cache, distributed cache)
- Profile with **BenchmarkDotNet** before optimizing
- Avoid **premature optimization** – measure first
- Use **LINQ efficiently** – understand when queries execute
- Consider **parallel processing** with `Parallel.ForEachAsync` for CPU-bound work

## 12. Security

### Security Best Practices

- Always **validate and sanitize input**
- Use **parameterized queries** to prevent SQL injection
- Implement **authentication and authorization** (JWT, OAuth, OpenID Connect)
- Apply **HTTPS everywhere** – enforce with HSTS
- Use **data protection APIs** for encrypting sensitive data
- Implement **CORS policies** carefully – don't use wildcard origins in production
- Apply **rate limiting and throttling**
- Use **secrets management** (Azure Key Vault, AWS Secrets Manager)
- Keep **dependencies up to date** – monitor for vulnerabilities
- Follow **OWASP Top 10** guidelines
- Use **API keys** for service-to-service authentication
- Implement **content security policies** (CSP headers)

## 13. Additional Guidelines

### General Code Quality

- Follow **SOLID principles**
- Apply **DRY (Don't Repeat Yourself)** – extract common logic
- Keep methods **small and focused** (single responsibility)
- Use **guard clauses** to reduce nesting
- Prefer **composition over inheritance**
- Write **self-documenting code** – comments explain why, not what
- Use **code analyzers** (StyleCop, Roslynator) and enforce rules
- Enable **nullable reference types** and handle nulls explicitly
- Use **expression-bodied members** for simple properties/methods
- Apply **readonly** when possible for immutability

### Code Reviews and Standards

- Use **.editorconfig** for consistent formatting
- Enable **code analysis** in the project file
- Follow **Microsoft's C# coding conventions**
- Run **code analysis** in CI/CD pipelines
- Use **SonarQube** or similar tools for code quality metrics

## 14. .NET Specific Recommendations

### When Working with .NET 8+

- Use **Native AOT** for performance-critical applications
- Leverage **minimal APIs** for microservices
- Use **keyed services** for multiple implementations
- Apply **interceptors** for cross-cutting concerns
- Use **time abstraction** (`TimeProvider`) for testable time-dependent code

### When Working with ASP.NET Core

- Use **middleware pipeline** appropriately
- Apply **endpoint filters** for cross-cutting concerns
- Use **IHostedService** or **BackgroundService** for background tasks
- Implement **health checks** with dependencies
- Use **gRPC** for internal service-to-service communication

### When Working with Azure

- Use **Azure SDK** libraries following Azure SDK guidelines
- Implement **retry policies** with Polly or built-in resilience
- Use **managed identities** instead of connection strings
- Leverage **Azure Monitor** and **Application Insights**
- Use **Azure App Configuration** for feature flags and configuration
