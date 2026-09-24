namespace CustomerApi.Application.CreateCustomer;

public sealed record CreateCustomerCommand(string? FirstName, string? LastName, string? Email);