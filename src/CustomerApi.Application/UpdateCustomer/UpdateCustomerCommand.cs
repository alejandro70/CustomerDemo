namespace CustomerApi.Application.UpdateCustomer;

public sealed record UpdateCustomerCommand(
    Guid Id,
    string? FirstName,
    string? LastName,
    string? Email,
    long? Version);
