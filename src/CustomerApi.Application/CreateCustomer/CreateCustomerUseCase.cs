using System.ComponentModel.DataAnnotations;
using CustomerApi.Application.Abstractions;
using CustomerApi.Domain;

namespace CustomerApi.Application.CreateCustomer;

public sealed class CreateCustomerUseCase(
    ICustomerStore customerStore,
    IClock clock,
    IIdentifierGenerator identifierGenerator)
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public async Task<CreateCustomerResult> ExecuteAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(command.Email);
        var errors = Validate(command, normalizedEmail);
        if (errors.Count > 0)
        {
            return CreateCustomerResult.ValidationFailure(errors);
        }

        if (await customerStore.GetByEmailAsync(normalizedEmail, cancellationToken) is not null)
        {
            return CreateCustomerResult.DuplicateEmail();
        }

        var customer = new Customer(
            identifierGenerator.NewId(),
            command.FirstName!,
            command.LastName!,
            normalizedEmail,
            clock.UtcNow);

        var addResult = await customerStore.AddAsync(customer, cancellationToken);
        return addResult == CustomerStoreAddResult.DuplicateEmail
            ? CreateCustomerResult.DuplicateEmail()
            : CreateCustomerResult.Success(customer);
    }

    private static IReadOnlyDictionary<string, string[]> Validate(
        CreateCustomerCommand command,
        string normalizedEmail)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(command.FirstName))
        {
            errors[nameof(command.FirstName)] = ["First name is required."];
        }

        if (string.IsNullOrWhiteSpace(command.LastName))
        {
            errors[nameof(command.LastName)] = ["Last name is required."];
        }

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            errors[nameof(command.Email)] = ["Email is required."];
        }
        else if (!EmailValidator.IsValid(normalizedEmail))
        {
            errors[nameof(command.Email)] = ["Email is invalid."];
        }

        return errors;
    }
}