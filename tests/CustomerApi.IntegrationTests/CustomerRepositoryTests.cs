using CustomerApi.Application.Abstractions;
using CustomerApi.Domain;
using CustomerApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerApi.IntegrationTests;

public sealed class CustomerRepositoryTests
{
    [Fact]
    public async Task AddAsync_EmailUniqueIndexCollision_ReturnsDuplicateEmail()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"customer-repository-{Guid.NewGuid():N}.db");
        await using var context = CreateContext(databasePath);
        await context.Database.MigrateAsync();

        var repository = new CustomerRepository(context);
        var first = new Customer(Guid.NewGuid(), "Jane", "Doe", "john@example.com", DateTimeOffset.UtcNow);
        var duplicate = new Customer(Guid.NewGuid(), "Janet", "Smith", "john@example.com", DateTimeOffset.UtcNow);

        var firstResult = await repository.AddAsync(first, default);
        var duplicateResult = await repository.AddAsync(duplicate, default);

        Assert.Equal(CustomerStoreAddResult.Added, firstResult);
        Assert.Equal(CustomerStoreAddResult.DuplicateEmail, duplicateResult);
        Assert.Single(context.Customers);
        Assert.Equal("Jane", context.Customers.Single().FirstName);
    }

    [Fact]
    public async Task AddAsync_ConcurrentNormalizedEmailAttempts_LeaveOneUnchangedRecord()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"customer-repository-{Guid.NewGuid():N}.db");
        await using (var setupContext = CreateContext(databasePath))
        {
            await setupContext.Database.MigrateAsync();
        }

        var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var addFirst = Task.Run(async () =>
        {
            await startGate.Task;
            await using var context = CreateContext(databasePath);
            var repository = new CustomerRepository(context);
            return await repository.AddAsync(
                new Customer(Guid.NewGuid(), "First", "Writer", "race@example.com", DateTimeOffset.UtcNow),
                default);
        });

        var addSecond = Task.Run(async () =>
        {
            await startGate.Task;
            await using var context = CreateContext(databasePath);
            var repository = new CustomerRepository(context);
            return await repository.AddAsync(
                new Customer(Guid.NewGuid(), "Second", "Writer", "race@example.com", DateTimeOffset.UtcNow),
                default);
        });

        startGate.SetResult();
        var results = await Task.WhenAll(addFirst, addSecond);

        Assert.Contains(CustomerStoreAddResult.Added, results);
        Assert.Contains(CustomerStoreAddResult.DuplicateEmail, results);

        await using var verificationContext = CreateContext(databasePath);
        var persisted = await verificationContext.Customers.Where(customer => customer.Email == "race@example.com").ToListAsync();
        Assert.Single(persisted);
        Assert.Contains(persisted[0].FirstName, new[] { "First", "Second" });
    }

    [Fact]
    public async Task AddAsync_NonEmailConstraintViolation_IsNotMappedToDuplicateEmail()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"customer-repository-{Guid.NewGuid():N}.db");
        await using var setupContext = CreateContext(databasePath);
        await setupContext.Database.MigrateAsync();

        var firstRepository = new CustomerRepository(setupContext);
        var id = Guid.NewGuid();

        var firstResult = await firstRepository.AddAsync(
            new Customer(id, "Original", "Customer", "original@example.com", DateTimeOffset.UtcNow),
            default);

        Assert.Equal(CustomerStoreAddResult.Added, firstResult);

        await using var collisionContext = CreateContext(databasePath);
        var collisionRepository = new CustomerRepository(collisionContext);

        await Assert.ThrowsAsync<DbUpdateException>(() => collisionRepository.AddAsync(
            new Customer(id, "Collision", "Customer", "different@example.com", DateTimeOffset.UtcNow),
            default));

        Assert.Single(setupContext.Customers);
        Assert.Equal("original@example.com", setupContext.Customers.Single().Email);
    }

    [Fact]
    public async Task MigrationSnapshot_HasNoPendingModelChanges()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"customer-repository-{Guid.NewGuid():N}.db");
        await using var context = CreateContext(databasePath);
        await context.Database.MigrateAsync();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static CustomerDbContext CreateContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new CustomerDbContext(options);
    }
}
