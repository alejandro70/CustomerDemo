using CustomerApi.Domain;

namespace CustomerApi.Application.UpdateCustomer;

public sealed class UpdateCustomerResult
{
    private UpdateCustomerResult(UpdateCustomerStatus status, Customer? customer, IReadOnlyDictionary<string, string[]> errors)
    {
        Status = status;
        Customer = customer;
        Errors = errors;
    }

    public UpdateCustomerStatus Status { get; }

    public Customer? Customer { get; }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public static UpdateCustomerResult Success(Customer customer) =>
        new(UpdateCustomerStatus.Success, customer, new Dictionary<string, string[]>());

    public static UpdateCustomerResult ValidationFailure(IReadOnlyDictionary<string, string[]> errors) =>
        new(UpdateCustomerStatus.ValidationFailure, null, errors);

    public static UpdateCustomerResult NotFound() =>
        new(UpdateCustomerStatus.NotFound, null, new Dictionary<string, string[]>());

    public static UpdateCustomerResult DuplicateEmail() =>
        new(UpdateCustomerStatus.DuplicateEmail, null, new Dictionary<string, string[]>());

    public static UpdateCustomerResult ConcurrencyConflict() =>
        new(UpdateCustomerStatus.ConcurrencyConflict, null, new Dictionary<string, string[]>());
}

public enum UpdateCustomerStatus
{
    Success,
    ValidationFailure,
    NotFound,
    DuplicateEmail,
    ConcurrencyConflict
}
