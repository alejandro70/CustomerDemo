using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace CustomerApi.IntegrationTests;

public sealed class CustomersEndpointTests : IClassFixture<CustomerApiFactory>
{
    private readonly CustomerApiFactory factory;

    public CustomersEndpointTests(CustomerApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task CreateAndRetrieve_ReturnExpectedCustomerRepresentation()
    {
        using var writeClient = factory.CreateAuthorizedClient("Customer.Write", "scp");
        var createResponse = await writeClient.PostAsJsonAsync("/customers", new { firstName = "John", lastName = "Doe", email = "  John@Example.COM  " });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("john@example.com", created.Email);

        using var readClient = factory.CreateAuthorizedClient("Customer.Read", "roles");
        var getResponse = await readClient.GetAsync($"/customers/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.Equal(created, retrieved);
    }

    [Fact]
    public async Task ErrorsAndAuthorization_ReturnRequiredStatuses()
    {
        using var anonymousClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymousClient.GetAsync($"/customers/{Guid.NewGuid()}")).StatusCode);

        using var unauthorizedClient = factory.CreateAuthorizedClient("Other.Permission", "scp");
        Assert.Equal(HttpStatusCode.Forbidden, (await unauthorizedClient.PostAsJsonAsync("/customers", new { firstName = "A", lastName = "B", email = "a@example.com" })).StatusCode);

        using var writeClient = factory.CreateAuthorizedClient("Customer.Write", "scp");
        Assert.Equal(HttpStatusCode.BadRequest, (await writeClient.PostAsJsonAsync("/customers", new { firstName = " ", lastName = "Doe", email = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await factory.CreateAuthorizedClient("Customer.Read", "scp").GetAsync("/customers/not-a-guid")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateAuthorizedClient("Customer.Read", "scp").GetAsync($"/customers/{Guid.NewGuid()}")).StatusCode);

        var first = await writeClient.PostAsJsonAsync("/customers", new { firstName = "Jane", lastName = "Doe", email = "duplicate@example.com" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var duplicate = await writeClient.PostAsJsonAsync("/customers", new { firstName = "Janet", lastName = "Doe", email = " DUPLICATE@example.com " });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("CUSTOMER_EMAIL_EXISTS", await duplicate.Content.ReadAsStringAsync());
    }

    private sealed record CustomerDto(Guid Id, string FirstName, string LastName, string Email, DateTimeOffset CreatedAt);
}

public sealed class CustomerApiFactory : WebApplicationFactory<Program>
{
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("customer-api-integration-test-signing-key-12345"));
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"customer-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:CustomerDatabase", $"Data Source={databasePath}");
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false;
                options.ConfigurationManager = null;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = "test-issuer",
                    ValidateAudience = true, ValidAudience = "customer-api",
                    ValidateIssuerSigningKey = true, IssuerSigningKey = SigningKey,
                    ValidateLifetime = true
                };
            });
        });
    }

    public HttpClient CreateAuthorizedClient(string permission, string claimType)
    {
        var client = CreateClient();
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "test-issuer", audience: "customer-api", claims: [new Claim(claimType, permission)],
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}