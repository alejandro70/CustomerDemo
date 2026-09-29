namespace CustomerApi.Domain;

public sealed class Customer
{
    public Customer(
        Guid id,
        string firstName,
        string lastName,
        string email,
        DateTimeOffset createdAt,
        long version = 1)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException("First name is required.", nameof(firstName));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException("Last name is required.", nameof(lastName));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (version <= 0)
        {
            throw new ArgumentException("Customer version must be positive.", nameof(version));
        }

        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        CreatedAt = createdAt.ToUniversalTime();
        Version = version;
    }

    public Guid Id { get; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string Email { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public long Version { get; private set; }

    public void UpdateProfile(string firstName, string lastName, string email)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new ArgumentException("First name is required.", nameof(firstName));
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new ArgumentException("Last name is required.", nameof(lastName));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Version++;
    }
}