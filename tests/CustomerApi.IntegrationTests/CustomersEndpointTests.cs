using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
        Assert.Equal("John", created.FirstName);
        Assert.Equal("Doe", created.LastName);
        Assert.Equal("john@example.com", created.Email);
        Assert.NotEqual(default, created.CreatedAt);
        Assert.Equal(TimeSpan.Zero, created.CreatedAt.Offset);
        Assert.EndsWith($"/customers/{created.Id}", createResponse.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        using var readClient = factory.CreateAuthorizedClient("Customer.Read", "roles");
        var getResponse = await readClient.GetAsync($"/customers/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.Equal(created, retrieved);
    }

    [Theory]
    [MemberData(nameof(InvalidCreateRequests))]
    public async Task Create_InvalidInput_Returns400_AndDoesNotMutate(string payload)
    {
        using var writeClient = factory.CreateAuthorizedClient("Customer.Write", "scp");
        var countBefore = await factory.CountCustomersAsync();

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await writeClient.PostAsync("/customers", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(countBefore, await factory.CountCustomersAsync());
    }

    [Fact]
    public async Task Get_InvalidGuid_Returns400()
    {
        using var client = factory.CreateAuthorizedClient("Customer.Read", "scp");

        var response = await client.GetAsync("/customers/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_MissingCustomer_Returns404()
    {
        using var client = factory.CreateAuthorizedClient("Customer.Read", "scp");

        var response = await client.GetAsync($"/customers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateEmail_Returns409_AndPreservesOriginalRecord()
    {
        using var writeClient = factory.CreateAuthorizedClient("Customer.Write", "scp");
        var countBefore = await factory.CountCustomersAsync();

        var first = await writeClient.PostAsJsonAsync("/customers", new { firstName = "Jane", lastName = "Doe", email = "duplicate@example.com" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await writeClient.PostAsJsonAsync("/customers", new { firstName = "Janet", lastName = "Smith", email = " DUPLICATE@example.com " });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var duplicateBody = await duplicate.Content.ReadAsStringAsync();
        Assert.Contains("CUSTOMER_EMAIL_EXISTS", duplicateBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SqliteException", duplicateBody, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(countBefore + 1, await factory.CountCustomersAsync());
        var persisted = await factory.GetCustomerByEmailAsync("duplicate@example.com");
        Assert.NotNull(persisted);
        Assert.Equal("Jane", persisted.FirstName);
        Assert.Equal("Doe", persisted.LastName);
    }

    [Fact]
    public async Task AnonymousRequests_Return401_AndDoNotMutateOrDisclose()
    {
        using var writeClient = factory.CreateAuthorizedClient("Customer.Write", "scp");
        var created = await writeClient.PostAsJsonAsync("/customers", new { firstName = "Alice", lastName = "Smith", email = "alice@example.com" });
        var createdCustomer = await created.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(createdCustomer);

        using var anonymousClient = factory.CreateClient();
        var beforeCount = await factory.CountCustomersAsync();

        var postResponse = await anonymousClient.PostAsJsonAsync("/customers", new { firstName = "Bob", lastName = "Jones", email = "bob@example.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
        Assert.Equal(beforeCount, await factory.CountCustomersAsync());

        var getResponse = await anonymousClient.GetAsync($"/customers/{createdCustomer.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);

        var getBody = await getResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(createdCustomer.Email, getBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(createdCustomer.Id.ToString(), getBody, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task InvalidTokenRequests_Return401_AndDoNotMutateOrDisclose(string invalidTokenKind)
    {
        var email = $"carol-{Guid.NewGuid():N}@example.com";
        using var writeClient = factory.CreateAuthorizedClient("Customer.Write", "scp");
        var created = await writeClient.PostAsJsonAsync("/customers", new { firstName = "Carol", lastName = "Wayne", email });
        var createdCustomer = await created.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(createdCustomer);

        var beforeCount = await factory.CountCustomersAsync();
        var token = CreateInvalidToken(invalidTokenKind);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var postResponse = await client.PostAsJsonAsync("/customers", new { firstName = "Dan", lastName = "Mills", email = "dan@example.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
        Assert.Equal(beforeCount, await factory.CountCustomersAsync());

        var getResponse = await client.GetAsync($"/customers/{createdCustomer.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
        var getBody = await getResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(createdCustomer.Email, getBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(createdCustomer.Id.ToString(), getBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingWritePermission_Returns403_AndDoesNotMutate()
    {
        var beforeCount = await factory.CountCustomersAsync();
        using var unauthorizedClient = factory.CreateAuthorizedClient("Customer.Read", "scp");
        var attemptedEmail = $"forbidden-{Guid.NewGuid():N}@example.com";

        var response = await unauthorizedClient.PostAsJsonAsync("/customers", new { firstName = "A", lastName = "B", email = attemptedEmail });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(beforeCount, await factory.CountCustomersAsync());

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(attemptedEmail, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingReadPermission_Returns403_AndDoesNotDiscloseData()
    {
        using var writeClient = factory.CreateAuthorizedClient("Customer.Write", "scp");
        var created = await writeClient.PostAsJsonAsync("/customers", new { firstName = "Eric", lastName = "Cole", email = "eric@example.com" });
        var createdCustomer = await created.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(createdCustomer);

        using var unauthorizedClient = factory.CreateAuthorizedClient("Customer.Write", "roles");
        var response = await unauthorizedClient.GetAsync($"/customers/{createdCustomer.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(createdCustomer.Email, responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(createdCustomer.Id.ToString(), responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Readiness_SucceedsAfterMigrationApplication()
    {
        using var client = factory.CreateClient();

        var readinessResponse = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
        var migrations = await factory.GetAppliedMigrationsAsync();
        Assert.Contains("20260924000000_InitialCustomer", migrations);
    }

    [Fact]
    public async Task Telemetry_EmitsOutcomeSignals_AndOmitsSensitiveValues()
    {
        const string sensitiveEmail = "sensitive@example.com";
        var bearerToken = factory.CreateToken("Customer.Read", "scp");

        using var writeClient = factory.CreateAuthorizedClient("Customer.Write", "scp");
        _ = await writeClient.PostAsJsonAsync("/customers", new { firstName = "Safe", lastName = "Logs", email = sensitiveEmail });
        _ = await writeClient.PostAsJsonAsync("/customers", new { firstName = " ", lastName = "Logs", email = sensitiveEmail });
        _ = await writeClient.PostAsJsonAsync("/customers", new { firstName = "Safe", lastName = "Logs", email = " SENSITIVE@example.com " });

        using var anonymousClient = factory.CreateClient();
        _ = await anonymousClient.GetAsync($"/customers/{Guid.NewGuid()}");

        using var forbiddenClient = factory.CreateAuthorizedClient("Customer.Read", "scp");
        _ = await forbiddenClient.PostAsJsonAsync("/customers", new { firstName = "No", lastName = "Write", email = "nowrite@example.com" });

        using var unhandledClient = factory.CreateAuthorizedClient("Customer.Read", "scp");
        var unhandledResponse = await unhandledClient.GetAsync("/testing/unhandled");
        Assert.Equal(HttpStatusCode.InternalServerError, unhandledResponse.StatusCode);

        var logs = factory.GetLogs();
        var measurements = factory.GetRequestOutcomeMeasurements();

        Assert.Contains(logs, message => message.Contains("success_or_other", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.Contains("validation_failure", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.Contains("duplicate_email_conflict", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.Contains("authentication_failure", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.Contains("authorization_failure", StringComparison.Ordinal));
        Assert.Contains(logs, message => message.Contains("unhandled_failure", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, message => message.Contains(sensitiveEmail, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, message => message.Contains("authorization", StringComparison.OrdinalIgnoreCase) && message.Contains("Bearer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, message => message.Contains(bearerToken, StringComparison.Ordinal));

        Assert.Contains(measurements, measurement =>
            measurement.Outcome == "success_or_other" &&
            measurement.StatusCode == (int)HttpStatusCode.Created);
        Assert.Contains(measurements, measurement =>
            measurement.Outcome == "validation_failure" &&
            measurement.StatusCode == (int)HttpStatusCode.BadRequest);
        Assert.Contains(measurements, measurement =>
            measurement.Outcome == "duplicate_email_conflict" &&
            measurement.StatusCode == (int)HttpStatusCode.Conflict);
        Assert.Contains(measurements, measurement =>
            measurement.Outcome == "authentication_failure" &&
            measurement.StatusCode == (int)HttpStatusCode.Unauthorized);
        Assert.Contains(measurements, measurement =>
            measurement.Outcome == "authorization_failure" &&
            measurement.StatusCode == (int)HttpStatusCode.Forbidden);
        Assert.Contains(measurements, measurement =>
            measurement.Outcome == "unhandled_failure" &&
            measurement.StatusCode == (int)HttpStatusCode.InternalServerError);

        Assert.DoesNotContain(measurements, measurement => measurement.Endpoint.Contains(sensitiveEmail, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(measurements, measurement => measurement.Endpoint.Contains("Bearer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(measurements, measurement => measurement.Endpoint.Contains(bearerToken, StringComparison.Ordinal));
    }

    public static IEnumerable<object[]> InvalidCreateRequests()
    {
        yield return ["{\"lastName\":\"Doe\",\"email\":\"john@example.com\"}"];
        yield return ["{\"firstName\":\"John\",\"email\":\"john@example.com\"}"];
        yield return ["{\"firstName\":\"John\",\"lastName\":\"Doe\"}"];
        yield return ["{\"firstName\":null,\"lastName\":\"Doe\",\"email\":\"john@example.com\"}"];
        yield return ["{\"firstName\":\"John\",\"lastName\":null,\"email\":\"john@example.com\"}"];
        yield return ["{\"firstName\":\"John\",\"lastName\":\"Doe\",\"email\":null}"];
        yield return ["{\"firstName\":\"   \",\"lastName\":\"Doe\",\"email\":\"john@example.com\"}"];
        yield return ["{\"firstName\":\"John\",\"lastName\":\"   \",\"email\":\"john@example.com\"}"];
        yield return ["{\"firstName\":\"John\",\"lastName\":\"Doe\",\"email\":\"not-an-email\"}"];
    }

    public static IEnumerable<object[]> InvalidTokens()
    {
        yield return ["malformed"];
        yield return ["wrong-issuer"];
        yield return ["wrong-audience"];
        yield return ["invalid-signature"];
        yield return ["expired"];
    }

    private string CreateInvalidToken(string invalidTokenKind) => invalidTokenKind switch
    {
        "malformed" => "not-a-jwt-token",
        "wrong-issuer" => factory.CreateToken("Customer.Write", "scp", issuer: "https://wrong-issuer"),
        "wrong-audience" => factory.CreateToken("Customer.Write", "scp", audience: "api://wrong-audience"),
        "invalid-signature" => factory.CreateToken(
            "Customer.Write",
            "scp",
            signingKey: new SymmetricSecurityKey(Encoding.UTF8.GetBytes("customer-api-other-signing-key-54321"))),
        "expired" => factory.CreateToken(
            "Customer.Write",
            "scp",
            notBeforeUtc: DateTime.UtcNow.AddMinutes(-10),
            expiresUtc: DateTime.UtcNow.AddMinutes(-5)),
        _ => throw new ArgumentOutOfRangeException(nameof(invalidTokenKind), invalidTokenKind, "Unsupported invalid-token case.")
    };

    private sealed record CustomerDto(Guid Id, string FirstName, string LastName, string Email, DateTimeOffset CreatedAt);
}