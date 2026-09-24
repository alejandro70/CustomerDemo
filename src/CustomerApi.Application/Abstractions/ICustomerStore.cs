using CustomerApi.Domain;

namespace CustomerApi.Application.Abstractions;

public interface ICustomerStore
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Customer?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<CustomerStoreAddResult> AddAsync(Customer customer, CancellationToken cancellationToken);
}

public enum CustomerStoreAddResult
{
    Added,
    DuplicateEmail
}