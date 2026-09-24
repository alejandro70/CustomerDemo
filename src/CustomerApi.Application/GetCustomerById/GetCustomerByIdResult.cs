using CustomerApi.Domain;

namespace CustomerApi.Application.GetCustomerById;

public sealed class GetCustomerByIdResult
{
    private GetCustomerByIdResult(Customer? customer)
    {
        Customer = customer;
    }

    public Customer? Customer { get; }

    public bool Found => Customer is not null;

    public static GetCustomerByIdResult FromCustomer(Customer? customer) => new(customer);
}