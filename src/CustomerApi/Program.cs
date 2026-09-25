using CustomerApi;
using CustomerApi.Application.Abstractions;
using CustomerApi.Application.CreateCustomer;
using CustomerApi.Application.GetCustomerById;
using CustomerApi.Authentication;
using CustomerApi.Infrastructure;
using CustomerApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("CustomerDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:CustomerDatabase must be configured.");

builder.Services
    .AddOptions<EntraAuthenticationOptions>()
    .BindConfiguration(EntraAuthenticationOptions.SectionName)
    .Validate(options => EntraAuthenticationOptions.IsValidAuthority(options.Authority),
        "Entra:Authority must be configured as an absolute https URI.")
    .Validate(options => EntraAuthenticationOptions.IsValidAudience(options.Audience),
        "Entra:Audience must be configured as a GUID or absolute URI.")
    .ValidateOnStart();

var authority = builder.Configuration[$"{EntraAuthenticationOptions.SectionName}:Authority"];
var audience = builder.Configuration[$"{EntraAuthenticationOptions.SectionName}:Audience"];

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<CustomerDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ICustomerStore, CustomerRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IIdentifierGenerator, GuidIdentifierGenerator>();
builder.Services.AddScoped<CreateCustomerUseCase>();
builder.Services.AddScoped<GetCustomerByIdUseCase>();
builder.Services.AddHealthChecks().AddDbContextCheck<CustomerDbContext>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.ValidateLifetime = true;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(CustomerPolicies.Write, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
        HasPermission(context.User, CustomerPolicies.Write)))
    .AddPolicy(CustomerPolicies.Read, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
        HasPermission(context.User, CustomerPolicies.Read)));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    app.Logger.LogInformation("Applying EF Core migrations before accepting traffic.");
    await scope.ServiceProvider.GetRequiredService<CustomerDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var endpoint = GetEndpointLabel(context);

    try
    {
        await next(context);
        EmitOutcome(context, endpoint, context.Response.StatusCode);
    }
    catch
    {
        EmitOutcome(context, endpoint, StatusCodes.Status500InternalServerError);
        throw;
    }
});
app.UseAuthorization();
app.MapCustomerEndpoints();

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet("/testing/unhandled", (HttpContext _) => throw new InvalidOperationException("Simulated unhandled failure for integration testing."));
}

app.MapHealthChecks("/health/ready");

app.Run();

static bool HasPermission(System.Security.Claims.ClaimsPrincipal user, string requiredPermission) =>
    user.Claims.Any(claim =>
        (claim.Type is "scp" or "http://schemas.microsoft.com/identity/claims/scope") &&
        claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(requiredPermission, StringComparer.Ordinal) ||
        (claim.Type is "roles" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") &&
        string.Equals(claim.Value, requiredPermission, StringComparison.Ordinal));

static void EmitOutcome(HttpContext context, string endpoint, int statusCode)
{
    var outcome = RequestOutcomeClassifier.Classify(statusCode);
    RequestOutcomeMetrics.Record(outcome, endpoint, statusCode);
    context.RequestServices
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("CustomerApi.RequestOutcomes")
        .LogInformation(
            "Customer API request outcome {Outcome} with status {StatusCode} for endpoint {Endpoint} and trace {TraceId}",
            outcome,
            statusCode,
            endpoint,
            context.TraceIdentifier);
}

static string GetEndpointLabel(HttpContext context)
{
    if (context.GetEndpoint() is RouteEndpoint routeEndpoint)
    {
        return routeEndpoint.RoutePattern.RawText ?? routeEndpoint.DisplayName ?? "unmatched";
    }

    return context.GetEndpoint()?.DisplayName ?? "unmatched";
}

public partial class Program
{
}
