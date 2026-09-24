using CustomerApi.Application.Abstractions;
using CustomerApi.Application.CreateCustomer;
using CustomerApi.Application.GetCustomerById;
using CustomerApi.Domain;

namespace CustomerApi.Tests;

public sealed class CustomerUseCaseTests
{
    [Fact]
    public async Task Create_PersistsNormalizedCustomerWithServerAssignedValues()
    {
        var store = new FakeCustomerStore();
        var id = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        var useCase = new CreateCustomerUseCase(store, new FakeClock(now), new FakeIdentifierGenerator(id));

        var result = await useCase.ExecuteAsync(new CreateCustomerCommand("John", "Doe", "  John@Example.COM  "), default);

        Assert.Equal(CreateCustomerStatus.Success, result.Status);
        Assert.NotNull(result.Customer);
        Assert.Equal(id, result.Customer.Id);
        Assert.Equal(now, result.Customer.CreatedAt);
        Assert.Equal("john@example.com", result.Customer.Email);
        Assert.Single(store.Customers);
    }

    [Theory]
    [InlineData(null, "Doe", "john@example.com")]
    [InlineData("   ", "Doe", "john@example.com")]
    [InlineData("John", null, "john@example.com")]
    [InlineData("John", "   ", "john@example.com")]
    [InlineData("John", "Doe", null)]
    [InlineData("John", "Doe", "not-an-email")]
    public async Task Create_InvalidInputDoesNotWrite(string? firstName, string? lastName, string? email)
    {
        var store = new FakeCustomerStore();
        var useCase = new CreateCustomerUseCase(store, new FakeClock(DateTimeOffset.UtcNow), new FakeIdentifierGenerator(Guid.NewGuid()));

        var result = await useCase.ExecuteAsync(new CreateCustomerCommand(firstName, lastName, email), default);

        Assert.Equal(CreateCustomerStatus.ValidationFailure, result.Status);
        Assert.Empty(store.Customers);
    }

    [Fact]
    public async Task Create_NormalizedDuplicateDoesNotWrite()
    {
        var store = new FakeCustomerStore();
        store.Customers.Add(new Customer(Guid.NewGuid(), "Jane", "Doe", "john@example.com", DateTimeOffset.UtcNow));
        var useCase = new CreateCustomerUseCase(store, new FakeClock(DateTimeOffset.UtcNow), new FakeIdentifierGenerator(Guid.NewGuid()));

        var result = await useCase.ExecuteAsync(new CreateCustomerCommand("John", "Doe", "  JOHN@example.com "), default);

        Assert.Equal(CreateCustomerStatus.DuplicateEmail, result.Status);
        Assert.Single(store.Customers);
    }

    [Fact]
    public async Task GetById_ReturnsFoundAndMissingOutcomes()
    {
        var customer = new Customer(Guid.NewGuid(), "John", "Doe", "john@example.com", DateTimeOffset.UtcNow);
        var store = new FakeCustomerStore();
        store.Customers.Add(customer);
        var useCase = new GetCustomerByIdUseCase(store);

        var found = await useCase.ExecuteAsync(customer.Id, default);
        var missing = await useCase.ExecuteAsync(Guid.NewGuid(), default);

        Assert.True(found.Found);
        Assert.Equal(customer, found.Customer);
        Assert.False(missing.Found);
    }

    private sealed class FakeCustomerStore : ICustomerStore
    {
        public List<Customer> Customers { get; } = [];

        public Task<CustomerStoreAddResult> AddAsync(Customer customer, CancellationToken cancellationToken)
        {
            if (Customers.Any(existing => existing.Email == customer.Email))
            {
                return Task.FromResult(CustomerStoreAddResult.DuplicateEmail);
            }

            Customers.Add(customer);
            return Task.FromResult(CustomerStoreAddResult.Added);
        }

        public Task<Customer?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(Customers.SingleOrDefault(customer => customer.Email == normalizedEmail));

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Customers.SingleOrDefault(customer => customer.Id == id));
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class FakeIdentifierGenerator(Guid id) : IIdentifierGenerator
    {
        public Guid NewId() => id;
    }
}
