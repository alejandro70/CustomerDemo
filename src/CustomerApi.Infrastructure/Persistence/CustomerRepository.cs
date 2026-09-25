using CustomerApi.Application.Abstractions;
using CustomerApi.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CustomerApi.Infrastructure.Persistence;

public sealed class CustomerRepository(CustomerDbContext dbContext) : ICustomerStore
{
    public const string EmailUniqueIndexName = "IX_Customers_Email";

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Customers.SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public Task<Customer?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Customers.SingleOrDefaultAsync(customer => customer.Email == normalizedEmail, cancellationToken);

    public async Task<CustomerStoreAddResult> AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        dbContext.Customers.Add(customer);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return CustomerStoreAddResult.Added;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(customer).State = EntityState.Detached;
            return CustomerStoreAddResult.DuplicateEmail;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is not SqliteException sqliteException)
        {
            return false;
        }

        return sqliteException.SqliteErrorCode == 19 &&
               sqliteException.SqliteExtendedErrorCode == 2067 &&
               (sqliteException.Message.Contains("Customers.Email", StringComparison.OrdinalIgnoreCase) ||
                sqliteException.Message.Contains(EmailUniqueIndexName, StringComparison.OrdinalIgnoreCase));
    }
}