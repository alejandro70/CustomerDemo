using CustomerApi.Application.Abstractions;
using CustomerApi.Domain;
using CustomerApi.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
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
    public async Task UpdateAsync_ConcurrentTrackedUpdates_ReturnsConcurrencyConflictForStaleWriter()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"customer-repository-{Guid.NewGuid():N}.db");
        await using (var setupContext = CreateContext(databasePath))
        {
            await setupContext.Database.MigrateAsync();
            var setupRepository = new CustomerRepository(setupContext);
            var created = new Customer(Guid.NewGuid(), "Jane", "Doe", "jane@example.com", DateTimeOffset.UtcNow);
            var addResult = await setupRepository.AddAsync(created, default);
            Assert.Equal(CustomerStoreAddResult.Added, addResult);
        }

        await using var firstContext = CreateContext(databasePath);
        await using var secondContext = CreateContext(databasePath);

        var firstRepository = new CustomerRepository(firstContext);
        var secondRepository = new CustomerRepository(secondContext);

        var firstTracked = await firstRepository.GetByEmailAsync("jane@example.com", default);
        var secondTracked = await secondRepository.GetByEmailAsync("jane@example.com", default);
        Assert.NotNull(firstTracked);
        Assert.NotNull(secondTracked);

        firstTracked.UpdateProfile("Janet", "Writer", "janet@example.com");
        secondTracked.UpdateProfile("Jane", "Writer", "jane.writer@example.com");

        var firstUpdateResult = await firstRepository.UpdateAsync(firstTracked, default);
        var secondUpdateResult = await secondRepository.UpdateAsync(secondTracked, default);

        Assert.Equal(CustomerStoreUpdateResult.Updated, firstUpdateResult);
        Assert.Equal(CustomerStoreUpdateResult.ConcurrencyConflict, secondUpdateResult);

        await using var verificationContext = CreateContext(databasePath);
        var persisted = await verificationContext.Customers.SingleAsync();
        Assert.Equal("Janet", persisted.FirstName);
        Assert.Equal("Writer", persisted.LastName);
        Assert.Equal("janet@example.com", persisted.Email);
        Assert.Equal(2, persisted.Version);
    }

    [Fact]
    public async Task UpdateAsync_EmailUniqueIndexCollision_ReturnsDuplicateEmail()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"customer-repository-{Guid.NewGuid():N}.db");
        await using var context = CreateContext(databasePath);
        await context.Database.MigrateAsync();
        var repository = new CustomerRepository(context);

        var first = new Customer(Guid.NewGuid(), "Jane", "Doe", "jane@example.com", DateTimeOffset.UtcNow);
        var second = new Customer(Guid.NewGuid(), "John", "Smith", "john@example.com", DateTimeOffset.UtcNow);

        Assert.Equal(CustomerStoreAddResult.Added, await repository.AddAsync(first, default));
        Assert.Equal(CustomerStoreAddResult.Added, await repository.AddAsync(second, default));

        var tracked = await repository.GetByIdAsync(second.Id, default);
        Assert.NotNull(tracked);
        tracked.UpdateProfile("John", "Smith", "jane@example.com");

        var updateResult = await repository.UpdateAsync(tracked, default);

        Assert.Equal(CustomerStoreUpdateResult.DuplicateEmail, updateResult);
        var persisted = await context.Customers.SingleAsync(customer => customer.Id == second.Id);
        Assert.Equal("john@example.com", persisted.Email);
        Assert.Equal(1, persisted.Version);
    }

    [Fact]
    public async Task MigrationSnapshot_HasNoPendingModelChanges()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"customer-repository-{Guid.NewGuid():N}.db");
        await using var context = CreateContext(databasePath);
        await context.Database.MigrateAsync();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task VersionMigration_ExistingCustomerGetsVersionOne_AndCanBeUpdated()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"customer-repository-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        await using (var context = CreateContext(databasePath))
        {
            await context.Database.MigrateAsync("20260924000000_InitialCustomer");
        }

        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        await using (var sqliteConnection = new SqliteConnection(connectionString))
        {
            await sqliteConnection.OpenAsync();
            await using var command = sqliteConnection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Customers (Id, FirstName, LastName, Email, CreatedAt)
                VALUES ($id, $firstName, $lastName, $email, $createdAt);";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$firstName", "Legacy");
            command.Parameters.AddWithValue("$lastName", "Customer");
            command.Parameters.AddWithValue("$email", "legacy@example.com");
            command.Parameters.AddWithValue("$createdAt", createdAt);
            _ = await command.ExecuteNonQueryAsync();
        }

        await using var migratedContext = CreateContext(databasePath);
        await migratedContext.Database.MigrateAsync();

        var repository = new CustomerRepository(migratedContext);
        var customer = await repository.GetByEmailAsync("legacy@example.com", default);
        Assert.NotNull(customer);
        Assert.Equal(id, customer.Id);
        Assert.Equal(1, customer.Version);

        customer.UpdateProfile("Legacy", "Updated", "legacy.updated@example.com");
        var updateResult = await repository.UpdateAsync(customer, default);

        Assert.Equal(CustomerStoreUpdateResult.Updated, updateResult);

        var reloaded = await repository.GetByEmailAsync("legacy.updated@example.com", default);
        Assert.NotNull(reloaded);
        Assert.Equal(id, reloaded.Id);
        Assert.Equal("Legacy", reloaded.FirstName);
        Assert.Equal("Updated", reloaded.LastName);
        Assert.Equal("legacy.updated@example.com", reloaded.Email);
        Assert.Equal(2, reloaded.Version);
    }

    private static CustomerDbContext CreateContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        return new CustomerDbContext(options);
    }
}
