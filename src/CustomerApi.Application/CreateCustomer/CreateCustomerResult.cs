using CustomerApi.Domain;

namespace CustomerApi.Application.CreateCustomer;

public sealed class CreateCustomerResult
{
    private CreateCustomerResult(CreateCustomerStatus status, Customer? customer, IReadOnlyDictionary<string, string[]> errors)
    {
        Status = status;
        Customer = customer;
        Errors = errors;
    }

    public CreateCustomerStatus Status { get; }

    public Customer? Customer { get; }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public static CreateCustomerResult Success(Customer customer) =>
        new(CreateCustomerStatus.Success, customer, new Dictionary<string, string[]>());

    public static CreateCustomerResult ValidationFailure(IReadOnlyDictionary<string, string[]> errors) =>
        new(CreateCustomerStatus.ValidationFailure, null, errors);

    public static CreateCustomerResult DuplicateEmail() =>
        new(CreateCustomerStatus.DuplicateEmail, null, new Dictionary<string, string[]>());
}

public enum CreateCustomerStatus
{
    Success,
    ValidationFailure,
    DuplicateEmail
}