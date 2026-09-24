namespace CustomerApi.Domain;

public sealed class Customer
{
    public Customer(
        Guid id,
        string firstName,
        string lastName,
        string email,
        DateTimeOffset createdAt)
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

        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public string Email { get; }

    public DateTimeOffset CreatedAt { get; }
}