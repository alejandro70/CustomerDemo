using System.ComponentModel.DataAnnotations;
using CustomerApi.Application.Abstractions;
using CustomerApi.Domain;

namespace CustomerApi.Application.UpdateCustomer;

public sealed class UpdateCustomerUseCase(ICustomerStore customerStore)
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    public async Task<UpdateCustomerResult> ExecuteAsync(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = EmailNormalizer.Normalize(command.Email);
        var errors = Validate(command, normalizedEmail);
        if (errors.Count > 0)
        {
            return UpdateCustomerResult.ValidationFailure(errors);
        }

        var customer = await customerStore.GetByIdAsync(command.Id, cancellationToken);
        if (customer is null)
        {
            return UpdateCustomerResult.NotFound();
        }

        if (customer.Version != command.Version)
        {
            return UpdateCustomerResult.ConcurrencyConflict();
        }

        var existingByEmail = await customerStore.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existingByEmail is not null && existingByEmail.Id != customer.Id)
        {
            return UpdateCustomerResult.DuplicateEmail();
        }

        customer.UpdateProfile(command.FirstName!, command.LastName!, normalizedEmail);
        var updateResult = await customerStore.UpdateAsync(customer, cancellationToken);

        return updateResult switch
        {
            CustomerStoreUpdateResult.Updated => UpdateCustomerResult.Success(customer),
            CustomerStoreUpdateResult.DuplicateEmail => UpdateCustomerResult.DuplicateEmail(),
            CustomerStoreUpdateResult.ConcurrencyConflict => UpdateCustomerResult.ConcurrencyConflict(),
            _ => throw new InvalidOperationException($"Unsupported customer update result: {updateResult}.")
        };
    }

    private static IReadOnlyDictionary<string, string[]> Validate(
        UpdateCustomerCommand command,
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

        if (!command.Version.HasValue)
        {
            errors[nameof(command.Version)] = ["Customer version is required."];
        }

        return errors;
    }
}
