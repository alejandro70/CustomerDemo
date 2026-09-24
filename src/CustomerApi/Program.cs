using CustomerApi;
using CustomerApi.Application.Abstractions;
using CustomerApi.Application.CreateCustomer;
using CustomerApi.Application.GetCustomerById;
using CustomerApi.Infrastructure;
using CustomerApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("CustomerDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:CustomerDatabase must be configured.");
var authority = builder.Configuration["Entra:Authority"]
    ?? throw new InvalidOperationException("Entra:Authority must be configured.");
var audience = builder.Configuration["Entra:Audience"]
    ?? throw new InvalidOperationException("Entra:Audience must be configured.");

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
    await scope.ServiceProvider.GetRequiredService<CustomerDbContext>().Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    await next(context);
    app.Logger.LogInformation(
        "Customer API request completed with status {StatusCode} for endpoint {Endpoint} and trace {TraceId}",
        context.Response.StatusCode,
        context.GetEndpoint()?.DisplayName ?? "unmatched",
        context.TraceIdentifier);
});
    app.UseAuthorization();
app.MapCustomerEndpoints();
app.MapHealthChecks("/health/ready");

app.Run();

static bool HasPermission(System.Security.Claims.ClaimsPrincipal user, string requiredPermission) =>
    user.Claims.Any(claim =>
        (claim.Type is "scp" or "http://schemas.microsoft.com/identity/claims/scope") &&
        claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(requiredPermission, StringComparer.Ordinal) ||
        (claim.Type is "roles" or "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") &&
        string.Equals(claim.Value, requiredPermission, StringComparison.Ordinal));

public partial class Program
{
}
