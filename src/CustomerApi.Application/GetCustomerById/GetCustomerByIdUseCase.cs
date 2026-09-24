using CustomerApi.Application.Abstractions;

namespace CustomerApi.Application.GetCustomerById;

public sealed class GetCustomerByIdUseCase(ICustomerStore customerStore)
{
    public async Task<GetCustomerByIdResult> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customerStore.GetByIdAsync(id, cancellationToken);
        return GetCustomerByIdResult.FromCustomer(customer);
    }
}