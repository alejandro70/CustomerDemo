using CustomerApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomerApi.Infrastructure.Persistence;

public sealed class CustomerDbContext(DbContextOptions<CustomerDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var customer = modelBuilder.Entity<Customer>();
        customer.ToTable("Customers");
        customer.HasKey(item => item.Id);
        customer.Property(item => item.FirstName).IsRequired();
        customer.Property(item => item.LastName).IsRequired();
        customer.Property(item => item.Email).IsRequired();
        customer.Property(item => item.CreatedAt).IsRequired();
        customer.HasIndex(item => item.Email)
            .HasDatabaseName(CustomerRepository.EmailUniqueIndexName)
            .IsUnique();
    }
}