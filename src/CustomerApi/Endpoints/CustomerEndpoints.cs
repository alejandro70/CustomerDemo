using CustomerApi.Application.CreateCustomer;
using CustomerApi.Application.GetCustomerById;
using CustomerApi.Domain;

namespace CustomerApi;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/customers", CreateAsync).RequireAuthorization(CustomerPolicies.Write);
        endpoints.MapGet("/customers/{id}", GetAsync).RequireAuthorization(CustomerPolicies.Read);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateCustomerRequest request,
        CreateCustomerUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new CreateCustomerCommand(request.FirstName, request.LastName, request.Email),
            cancellationToken);

        return result.Status switch
        {
            CreateCustomerStatus.Success => Results.Created(
                $"/customers/{result.Customer!.Id}",
                CustomerResponse.FromCustomer(result.Customer)),
            CreateCustomerStatus.ValidationFailure => Results.ValidationProblem(result.Errors),
            CreateCustomerStatus.DuplicateEmail => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Customer email already exists.",
                extensions: new Dictionary<string, object?> { ["code"] = "CUSTOMER_EMAIL_EXISTS" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static async Task<IResult> GetAsync(
        string id,
        GetCustomerByIdUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var customerId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["id"] = ["Customer id must be a GUID."]
            });
        }

        var result = await useCase.ExecuteAsync(customerId, cancellationToken);
        return result.Found
            ? Results.Ok(CustomerResponse.FromCustomer(result.Customer!))
            : Results.NotFound();
    }
}

public sealed record CreateCustomerRequest(string? FirstName, string? LastName, string? Email);

public sealed record CustomerResponse(Guid Id, string FirstName, string LastName, string Email, DateTimeOffset CreatedAt)
{
    public static CustomerResponse FromCustomer(Customer customer) =>
        new(customer.Id, customer.FirstName, customer.LastName, customer.Email, customer.CreatedAt);
}