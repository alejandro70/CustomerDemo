using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Diagnostics.Metrics;
using CustomerApi.Domain;
using CustomerApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CustomerApi.IntegrationTests;

public sealed class CustomerApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "https://entra.test/tenant/v2.0";
    public const string Audience = "api://customer-api";

    private static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("customer-api-integration-test-signing-key-12345"));

    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"customer-api-{Guid.NewGuid():N}.db");
    private readonly List<string> logs = [];
    private readonly List<RequestOutcomeMeasurement> requestOutcomeMeasurements = [];
    private readonly object requestOutcomeLock = new();
    private readonly MeterListener requestOutcomeListener;

    public CustomerApiFactory()
    {
        requestOutcomeListener = new MeterListener();
        requestOutcomeListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == RequestOutcomeMetrics.MeterName &&
                instrument.Name == RequestOutcomeMetrics.CounterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        requestOutcomeListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            var outcome = GetTagValue(tags, "outcome");
            var endpoint = GetTagValue(tags, "endpoint");
            var statusCodeText = GetTagValue(tags, "status_code");
            var statusCode = int.TryParse(statusCodeText, out var parsedStatusCode)
                ? parsedStatusCode
                : 0;

            lock (requestOutcomeLock)
            {
                requestOutcomeMeasurements.Add(new RequestOutcomeMeasurement(outcome, endpoint, statusCode, measurement));
            }
        });

        requestOutcomeListener.Start();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:CustomerDatabase", $"Data Source={databasePath}");
        builder.UseSetting("Entra:Authority", Issuer);
        builder.UseSetting("Entra:Audience", Audience);

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new ListLoggerProvider(logs));
        });

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                var openIdConfiguration = new OpenIdConnectConfiguration { Issuer = Issuer };
                openIdConfiguration.SigningKeys.Add(SigningKey);
                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(openIdConfiguration);
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.ValidateLifetime = true;
            });
        });
    }

    public HttpClient CreateAuthorizedClient(string permission, string claimType)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(permission, claimType));
        return client;
    }

    public string CreateToken(
        string permission,
        string claimType,
        DateTime? notBeforeUtc = null,
        DateTime? expiresUtc = null,
        string? issuer = null,
        string? audience = null,
        SecurityKey? signingKey = null)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            issuer: issuer ?? Issuer,
            audience: audience ?? Audience,
            claims: [new Claim(claimType, permission)],
            notBefore: notBeforeUtc ?? DateTime.UtcNow.AddMinutes(-1),
            expires: expiresUtc ?? DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.HmacSha256));

        return handler.WriteToken(token);
    }

    public async Task<int> CountCustomersAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        return await dbContext.Customers.CountAsync();
    }

    public async Task<Customer?> GetCustomerByEmailAsync(string normalizedEmail)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        return await dbContext.Customers.SingleOrDefaultAsync(customer => customer.Email == normalizedEmail);
    }

    public async Task<IReadOnlyList<string>> GetAppliedMigrationsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
        return (await dbContext.Database.GetAppliedMigrationsAsync()).ToList();
    }

    public IReadOnlyList<string> GetLogs() => logs.AsReadOnly();

    public IReadOnlyList<RequestOutcomeMeasurement> GetRequestOutcomeMeasurements()
    {
        lock (requestOutcomeLock)
        {
            return requestOutcomeMeasurements.ToList();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            requestOutcomeListener.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string GetTagValue(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
    {
        foreach (var tag in tags)
        {
            if (string.Equals(tag.Key, key, StringComparison.Ordinal))
            {
                return tag.Value?.ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private sealed class ListLoggerProvider(List<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ListLogger(logs);

        public void Dispose()
        {
        }
    }

    private sealed class ListLogger(List<string> logs) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            logs.Add(formatter(state, exception));
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    public sealed record RequestOutcomeMeasurement(string Outcome, string Endpoint, int StatusCode, long Count);
}
