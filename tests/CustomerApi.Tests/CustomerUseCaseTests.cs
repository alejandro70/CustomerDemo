using CustomerApi.Application.Abstractions;
using CustomerApi.Application.CreateCustomer;
using CustomerApi.Application.GetCustomerById;
using CustomerApi.Application.UpdateCustomer;
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
        Assert.Equal(1, result.Customer.Version);
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
        Assert.NotNull(found.Customer);
        Assert.Equal(customer.Id, found.Customer.Id);
        Assert.Equal(customer.FirstName, found.Customer.FirstName);
        Assert.Equal(customer.LastName, found.Customer.LastName);
        Assert.Equal(customer.Email, found.Customer.Email);
        Assert.Equal(customer.CreatedAt, found.Customer.CreatedAt);
        Assert.Equal(customer.Version, found.Customer.Version);
        Assert.False(missing.Found);
    }

    [Fact]
    public async Task Update_Success_PersistsNormalizedProfile_AndIncrementsVersion()
    {
        var store = new FakeCustomerStore();
        var customer = new Customer(Guid.NewGuid(), "Jane", "Doe", "jane@example.com", DateTimeOffset.UtcNow);
        store.Customers.Add(customer);
        var useCase = new UpdateCustomerUseCase(store);

        var result = await useCase.ExecuteAsync(
            new UpdateCustomerCommand(customer.Id, "Janet", "Smith", "  JANET@Example.COM  ", customer.Version),
            default);

        Assert.Equal(UpdateCustomerStatus.Success, result.Status);
        Assert.NotNull(result.Customer);
        Assert.Equal("Janet", result.Customer.FirstName);
        Assert.Equal("Smith", result.Customer.LastName);
        Assert.Equal("janet@example.com", result.Customer.Email);
        Assert.Equal(2, result.Customer.Version);

        var persisted = await store.GetByIdAsync(customer.Id, default);
        Assert.NotNull(persisted);
        Assert.Equal("Janet", persisted.FirstName);
        Assert.Equal("Smith", persisted.LastName);
        Assert.Equal("janet@example.com", persisted.Email);
        Assert.Equal(2, persisted.Version);
    }

    [Fact]
    public async Task Update_MissingVersion_DoesNotWrite()
    {
        var store = new FakeCustomerStore();
        var customer = new Customer(Guid.NewGuid(), "Jane", "Doe", "jane@example.com", DateTimeOffset.UtcNow);
        store.Customers.Add(customer);
        var useCase = new UpdateCustomerUseCase(store);

        var result = await useCase.ExecuteAsync(
            new UpdateCustomerCommand(customer.Id, "Janet", "Smith", "janet@example.com", null),
            default);

        Assert.Equal(UpdateCustomerStatus.ValidationFailure, result.Status);
        var persisted = await store.GetByIdAsync(customer.Id, default);
        Assert.NotNull(persisted);
        Assert.Equal("Jane", persisted.FirstName);
        Assert.Equal("Doe", persisted.LastName);
        Assert.Equal("jane@example.com", persisted.Email);
        Assert.Equal(1, persisted.Version);
    }

    [Fact]
    public async Task Update_StaleVersion_DoesNotWrite()
    {
        var store = new FakeCustomerStore();
        var customer = new Customer(Guid.NewGuid(), "Jane", "Doe", "jane@example.com", DateTimeOffset.UtcNow);
        store.Customers.Add(customer);
        var useCase = new UpdateCustomerUseCase(store);

        var result = await useCase.ExecuteAsync(
            new UpdateCustomerCommand(customer.Id, "Janet", "Smith", "janet@example.com", 0),
            default);

        Assert.Equal(UpdateCustomerStatus.ConcurrencyConflict, result.Status);
        var persisted = await store.GetByIdAsync(customer.Id, default);
        Assert.NotNull(persisted);
        Assert.Equal("Jane", persisted.FirstName);
        Assert.Equal("Doe", persisted.LastName);
        Assert.Equal("jane@example.com", persisted.Email);
        Assert.Equal(1, persisted.Version);
    }

    [Fact]
    public async Task Update_DuplicateEmail_DoesNotWrite()
    {
        var store = new FakeCustomerStore();
        var primary = new Customer(Guid.NewGuid(), "Jane", "Doe", "jane@example.com", DateTimeOffset.UtcNow);
        var other = new Customer(Guid.NewGuid(), "John", "Smith", "john@example.com", DateTimeOffset.UtcNow);
        store.Customers.Add(primary);
        store.Customers.Add(other);
        var useCase = new UpdateCustomerUseCase(store);

        var result = await useCase.ExecuteAsync(
            new UpdateCustomerCommand(primary.Id, "Jane", "Doe", " JOHN@example.com ", primary.Version),
            default);

        Assert.Equal(UpdateCustomerStatus.DuplicateEmail, result.Status);
        var persisted = await store.GetByIdAsync(primary.Id, default);
        Assert.NotNull(persisted);
        Assert.Equal("jane@example.com", persisted.Email);
        Assert.Equal(1, persisted.Version);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        var store = new FakeCustomerStore();
        var useCase = new UpdateCustomerUseCase(store);

        var result = await useCase.ExecuteAsync(
            new UpdateCustomerCommand(Guid.NewGuid(), "Jane", "Doe", "jane@example.com", 1),
            default);

        Assert.Equal(UpdateCustomerStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Update_StoreConflict_DoesNotWrite()
    {
        var store = new FakeCustomerStore { ForceConcurrencyConflictOnUpdate = true };
        var customer = new Customer(Guid.NewGuid(), "Jane", "Doe", "jane@example.com", DateTimeOffset.UtcNow);
        store.Customers.Add(customer);
        var useCase = new UpdateCustomerUseCase(store);

        var result = await useCase.ExecuteAsync(
            new UpdateCustomerCommand(customer.Id, "Janet", "Smith", "janet@example.com", customer.Version),
            default);

        Assert.Equal(UpdateCustomerStatus.ConcurrencyConflict, result.Status);
        var persisted = await store.GetByIdAsync(customer.Id, default);
        Assert.NotNull(persisted);
        Assert.Equal("Jane", persisted.FirstName);
        Assert.Equal("Doe", persisted.LastName);
        Assert.Equal("jane@example.com", persisted.Email);
        Assert.Equal(1, persisted.Version);
    }

    private sealed class FakeCustomerStore : ICustomerStore
    {
        public List<Customer> Customers { get; } = [];

        public bool ForceConcurrencyConflictOnUpdate { get; set; }

        public Task<CustomerStoreAddResult> AddAsync(Customer customer, CancellationToken cancellationToken)
        {
            if (Customers.Any(existing => existing.Email == customer.Email))
            {
                return Task.FromResult(CustomerStoreAddResult.DuplicateEmail);
            }

            Customers.Add(customer);
            return Task.FromResult(CustomerStoreAddResult.Added);
        }

        public Task<CustomerStoreUpdateResult> UpdateAsync(Customer customer, CancellationToken cancellationToken)
        {
            if (ForceConcurrencyConflictOnUpdate)
            {
                return Task.FromResult(CustomerStoreUpdateResult.ConcurrencyConflict);
            }

            if (Customers.Any(existing => existing.Id != customer.Id && existing.Email == customer.Email))
            {
                return Task.FromResult(CustomerStoreUpdateResult.DuplicateEmail);
            }

            var index = Customers.FindIndex(existing => existing.Id == customer.Id);
            if (index < 0)
            {
                return Task.FromResult(CustomerStoreUpdateResult.ConcurrencyConflict);
            }

            var current = Customers[index];
            if (customer.Version <= current.Version)
            {
                return Task.FromResult(CustomerStoreUpdateResult.ConcurrencyConflict);
            }

            Customers[index] = Clone(customer);
            return Task.FromResult(CustomerStoreUpdateResult.Updated);
        }

        public Task<Customer?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(CloneOrNull(Customers.SingleOrDefault(customer => customer.Email == normalizedEmail)));

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(CloneOrNull(Customers.SingleOrDefault(customer => customer.Id == id)));

        private static Customer Clone(Customer customer) =>
            new(customer.Id, customer.FirstName, customer.LastName, customer.Email, customer.CreatedAt, customer.Version);

        private static Customer? CloneOrNull(Customer? customer) =>
            customer is null ? null : Clone(customer);
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