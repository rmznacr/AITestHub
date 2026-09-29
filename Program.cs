using AITestHub.Configuration;
using AITestHub.Data;
using AITestHub.Services;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Project <-> TestCase navigation cycles are silently ignored during serialization.
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        // Enums (Priority, TestType, TestStatus, ...) are serialized as strings in API responses.
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// PostgreSQL / EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// EVREN LLM API test case generator.
// ApiKey is read from configuration (e.g. the 'Evren__ApiKey' environment variable) and is never stored in source control.
builder.Services.Configure<EvrenOptions>(builder.Configuration.GetSection(EvrenOptions.SectionName));
builder.Services.AddHttpClient<EvrenChatClient>()
    .ConfigureHttpClient((sp, client) =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EvrenOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(120);
    });
builder.Services.AddScoped<ITestCaseGeneratorService, EvrenTestCaseGeneratorService>();
builder.Services.AddScoped<IApiTestCaseGeneratorService, EvrenApiTestCaseGeneratorService>();
builder.Services.AddSingleton<IUrlNormalizer, UrlNormalizer>();

// Automated API test execution.
// The executor sends real HTTP requests to user-provided endpoints:
// - 30 second timeout
// - automatic redirects disabled (SSRF protection: redirect targets are not followed)
// - endpoint security validation blocks private/local/metadata addresses
builder.Services.AddScoped<IDnsResolver, DnsResolver>();
builder.Services.AddScoped<IEndpointSecurityValidator, EndpointSecurityValidator>();
builder.Services.AddHttpClient<IApiTestExecutorService, ApiTestExecutorService>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false
    });
builder.Services.AddScoped<ITestExecutionService, TestExecutionService>();

// CORS: allow only the configured frontend origins (default: Vite dev server).
// Production origins can be configured via 'Cors:AllowedOrigins' (comma-separated) in configuration.
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5173" })
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "AITestHub API",
        Version = "v1",
        Description = "Backend for AI-powered REST API test case generation, management and execution."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Safe startup diagnostics for the EVREN configuration.
// Only in non-production environments; never logs the API key itself.
if (!app.Environment.IsProduction())
{
    var evrenOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<EvrenOptions>>().Value;
    var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    startupLogger.LogInformation(
        "EVREN configuration: BaseUrl={BaseUrl}, Model={Model}, ApiKeyConfigured={ApiKeyConfigured}, ApiKeyLength={ApiKeyLength}",
        evrenOptions.BaseUrl,
        evrenOptions.Model,
        !string.IsNullOrWhiteSpace(evrenOptions.ApiKey),
        evrenOptions.ApiKey.Length);
}

// Controlled startup migration (opt-in via the 'ApplyMigrationsAtStartup' configuration flag,
// e.g. in the Docker/production environment). Waits until the database is reachable before
// applying pending migrations; never drops or recreates anything.
if (app.Configuration.GetValue<bool?>("ApplyMigrationsAtStartup") == true)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    const int maxAttempts = 10;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            app.Logger.LogInformation("Database migrations applied successfully.");
            break;
        }
        catch (Exception) when (attempt < maxAttempts)
        {
            app.Logger.LogWarning(
                "Database is not ready yet (attempt {Attempt}/{MaxAttempts}); retrying in 3 seconds.",
                attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AITestHub API v1");
    });
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthorization();

app.MapControllers();

// Health check endpoint used by Docker/orchestration.
// Verifies the API is up and the database connection works. It NEVER calls
// the EVREN API or any other external service.
app.MapGet("/health", async (AppDbContext context, CancellationToken cancellationToken) =>
{
    var databaseHealthy = await context.Database.CanConnectAsync(cancellationToken);

    return databaseHealthy
        ? Results.Ok(new { status = "Healthy", database = "Connected" })
        : Results.Json(new { status = "Unhealthy", database = "Unavailable" }, statusCode: 503);
});

app.Run();

// Required so that WebApplicationFactory<Program> in the test project can reference the entry point.
public partial class Program { }
