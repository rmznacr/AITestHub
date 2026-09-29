using AITestHub.Data;
using AITestHub.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AITestHub.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory that boots the full AITestHub API in-memory (no real network port)
/// and replaces the PostgreSQL DbContext registration with an EF Core InMemory database.
///
/// Isolation strategy:
/// - The real development PostgreSQL database is never touched: all EF Core service
///   registrations of the application (Npgsql provider, options configurations) are removed
///   and swapped for InMemory inside ConfigureWebHost.
/// - Every factory instance gets a UNIQUE InMemory database name (Guid), and each xUnit test
///   creates its own factory instance. Therefore every test runs against a completely fresh,
///   private database -> tests cannot affect each other and no cleanup logic is needed.
/// </summary>
public class AITestHubApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"AITestHub-Test-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the application's EF Core registrations for AppDbContext
            // (EF Core 10 registers provider configuration via IDbContextOptionsConfiguration<>).
            var efDescriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(AppDbContext) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)))
                .ToList();

            foreach (var descriptor in efDescriptors)
            {
                services.Remove(descriptor);
            }

            // Register an isolated InMemory database instead.
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            // Replace the real EVREN LLM generator with the mock so that integration
            // tests never call the external API and do not require an API key.
            var generatorDescriptors = services
                .Where(d => d.ServiceType == typeof(ITestCaseGeneratorService))
                .ToList();

            foreach (var descriptor in generatorDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddScoped<ITestCaseGeneratorService, MockTestCaseGeneratorService>();

            // Replace the executable-API-test generator with the mock as well.
            var apiGeneratorDescriptors = services
                .Where(d => d.ServiceType == typeof(IApiTestCaseGeneratorService))
                .ToList();

            foreach (var descriptor in apiGeneratorDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddScoped<IApiTestCaseGeneratorService, MockApiTestCaseGeneratorService>();

            // Replace the real HTTP test executor with a fake so that integration
            // tests never send real network requests.
            var executorDescriptors = services
                .Where(d => d.ServiceType == typeof(IApiTestExecutorService))
                .ToList();

            foreach (var descriptor in executorDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IApiTestExecutorService>(new FakeApiTestExecutorService());

            // Replace the real DNS resolver so endpoint security validation works
            // in tests without any network access (any host resolves to a public IP).
            var dnsDescriptors = services
                .Where(d => d.ServiceType == typeof(IDnsResolver))
                .ToList();

            foreach (var descriptor in dnsDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IDnsResolver>(new FakeDnsResolver());

            // Keep test output clean.
            services.AddSingleton<ILoggerFactory>(new LoggerFactory());
        });
    }
}
