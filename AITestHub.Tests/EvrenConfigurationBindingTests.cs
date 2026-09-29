using AITestHub.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AITestHub.Tests;

/// <summary>
/// Regression tests proving that the 'Evren__ApiKey' environment variable is correctly
/// bound to EvrenOptions.ApiKey through the standard ASP.NET Core configuration pipeline
/// (environment variable keys use '__' as the section separator: Evren__ApiKey -> Evren:ApiKey).
/// </summary>
public class EvrenConfigurationBindingTests
{
    [Fact]
    public void EnvironmentVariable_Evren__ApiKey_ShouldBindToEvrenOptions_ApiKey()
    {
        Environment.SetEnvironmentVariable("Evren__ApiKey", "unit-test-key");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var options = configuration.GetSection(EvrenOptions.SectionName).Get<EvrenOptions>()
                          ?? new EvrenOptions();

            Assert.Equal("unit-test-key", options.ApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Evren__ApiKey", null);
        }
    }

    [Fact]
    public void EnvironmentVariable_WithoutEvren__ApiKey_ApiKeyShouldBeEmpty()
    {
        Environment.SetEnvironmentVariable("Evren__ApiKey", null);
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var options = configuration.GetSection(EvrenOptions.SectionName).Get<EvrenOptions>()
                          ?? new EvrenOptions();

            Assert.True(string.IsNullOrWhiteSpace(options.ApiKey));
        }
        finally
        {
            Environment.SetEnvironmentVariable("Evren__ApiKey", null);
        }
    }
}
