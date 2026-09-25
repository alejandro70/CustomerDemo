using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CustomerApi.IntegrationTests;

public sealed class StartupConfigurationTests
{
    [Fact]
    public void MissingAuthority_FailsFastWithDiagnostic()
    {
        using var factory = new StartupValidationFactory(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:CustomerDatabase"] = $"Data Source={Path.Combine(Path.GetTempPath(), $"customer-api-{Guid.NewGuid():N}.db")}",
                ["Entra:Authority"] = string.Empty,
                ["Entra:Audience"] = "api://customer-api"
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Entra:Authority", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidAuthority_FailsFastWithDiagnostic()
    {
        using var factory = new StartupValidationFactory(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:CustomerDatabase"] = $"Data Source={Path.Combine(Path.GetTempPath(), $"customer-api-{Guid.NewGuid():N}.db")}",
                ["Entra:Authority"] = "not-a-uri",
                ["Entra:Audience"] = "api://customer-api"
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("absolute https URI", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidAudience_FailsFastWithDiagnostic()
    {
        using var factory = new StartupValidationFactory(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:CustomerDatabase"] = $"Data Source={Path.Combine(Path.GetTempPath(), $"customer-api-{Guid.NewGuid():N}.db")}",
                ["Entra:Authority"] = "https://entra.test/tenant/v2.0",
                ["Entra:Audience"] = " "
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Entra:Audience", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MissingAudience_FailsFastWithDiagnostic()
    {
        using var factory = new StartupValidationFactory(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:CustomerDatabase"] = $"Data Source={Path.Combine(Path.GetTempPath(), $"customer-api-{Guid.NewGuid():N}.db")}",
                ["Entra:Authority"] = "https://entra.test/tenant/v2.0",
                ["Entra:Audience"] = string.Empty
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Entra:Audience", exception.ToString(), StringComparison.Ordinal);
    }

    private sealed class StartupValidationFactory(IDictionary<string, string?> settings) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach (var setting in settings)
            {
                builder.UseSetting(setting.Key, setting.Value);
            }
        }
    }
}
