using CustomerApi.Domain;

namespace CustomerApi.Domain.Tests;

public sealed class CustomerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingOrWhitespaceFirstName(string? firstName)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Customer(Guid.NewGuid(), firstName!, "Doe", "john@example.com", DateTimeOffset.UtcNow));

        Assert.Equal("firstName", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingOrWhitespaceLastName(string? lastName)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new Customer(Guid.NewGuid(), "John", lastName!, "john@example.com", DateTimeOffset.UtcNow));

        Assert.Equal("lastName", exception.ParamName);
    }

    [Fact]
    public void Constructor_RetainsAssignedIdAndUtcCreationTime()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 9, 24, 12, 30, 0, TimeSpan.FromHours(2));

        var customer = new Customer(id, "John", "Doe", "john@example.com", createdAt);

        Assert.Equal(id, customer.Id);
        Assert.Equal(createdAt.ToUniversalTime(), customer.CreatedAt);
        Assert.Equal(TimeSpan.Zero, customer.CreatedAt.Offset);
        Assert.Equal(1, customer.Version);
    }

    [Fact]
    public void UpdateProfile_ChangesMutableFields_AndIncrementsVersion()
    {
        var customer = new Customer(Guid.NewGuid(), "John", "Doe", "john@example.com", DateTimeOffset.UtcNow);
        var createdAt = customer.CreatedAt;

        customer.UpdateProfile("Jane", "Smith", "jane@example.com");

        Assert.Equal("Jane", customer.FirstName);
        Assert.Equal("Smith", customer.LastName);
        Assert.Equal("jane@example.com", customer.Email);
        Assert.Equal(createdAt, customer.CreatedAt);
        Assert.Equal(2, customer.Version);
    }

    [Fact]
    public void UpdateProfile_NoOpReplacement_StillIncrementsVersion()
    {
        var customer = new Customer(Guid.NewGuid(), "John", "Doe", "john@example.com", DateTimeOffset.UtcNow);

        customer.UpdateProfile("John", "Doe", "john@example.com");

        Assert.Equal("John", customer.FirstName);
        Assert.Equal("Doe", customer.LastName);
        Assert.Equal("john@example.com", customer.Email);
        Assert.Equal(2, customer.Version);
    }

    [Fact]
    public void Normalize_TrimsAndUsesInvariantLowercase()
    {
        var normalizedEmail = EmailNormalizer.Normalize("  John@Example.COM  ");

        Assert.Equal("john@example.com", normalizedEmail);
    }
}