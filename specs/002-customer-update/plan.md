# Customer Update Technical Plan

## Scope and Constraints

Implement the approved customer update operation without changing the existing
presentation/application/domain/infrastructure layering. The operation is
`PUT /customers/{id}`, requires `Customer.Write`, replaces `firstName`,
`lastName`, and `email`, and returns the updated customer with HTTP 200.

The approved specification requires an observable customer version but leaves
its technical mechanism to this plan. Existing create and retrieve behavior
must remain compatible; their customer JSON representations will receive an
additive `version` property so clients can obtain the value required by update.
Existing clients that ignore unknown JSON properties remain compatible.

## Technical Decisions

### PD-001: Use a numeric Customer `Version` concurrency token

Add a non-null `long Version` property to `Customer`. New customers start at
version `1`. A successful profile update increments it by one. The API exposes
that value as the JSON `version` member and receives it as the nullable JSON
`version` member of the update request.

A numeric value is appropriate because the service already uses EF Core with
SQLite and has no HTTP entity-tag, versioning, or token infrastructure to
reuse. Unlike a timestamp, it has no precision or clock concerns. Unlike an
application-generated GUID, it does not require a new generator or persistence
convention. It is an implementation detail only insofar as it is the opaque
value a client must round-trip; clients must not derive a value themselves.

### PD-002: Use EF Core optimistic concurrency with the tracked aggregate

Configure `Customer.Version` with EF Core's `IsConcurrencyToken()`. Keep
`GetByIdAsync` as a tracking query. After loading the aggregate with its
original version, mutate it through a domain method that updates the permitted
profile fields and increments `Version`; `SaveChangesAsync` will then issue an
update constrained by the original version. EF Core throws
`DbUpdateConcurrencyException` when that update affects no row because another
request already changed the same customer.

This uses the existing `CustomerRepository` and `DbContext` rather than adding
an HTTP ETag abstraction, a separate concurrency service, or a new persistence
pattern. The repository catches the exception, clears/detaches the failed
tracked state, and returns a typed concurrency-conflict result. It continues to
translate SQLite's email unique-index exception to the existing typed
duplicate-email result.

### PD-003: Use the existing JSON-record endpoint convention for the version

Add `Version` to `CustomerResponse` and populate it from the domain entity for
create, retrieve, and update responses. Define `UpdateCustomerRequest` as a
record with nullable `FirstName`, `LastName`, `Email`, and `long? Version`.
`null` is the unambiguous missing-version case and maps to the required HTTP
400 validation response. Normal minimal-API JSON binding continues to handle
malformed JSON or incompatible value types using the service's existing 400
behavior.

The update request contains no `Id`; only the route identifier selects the
customer. This preserves the existing protection against identity changes.

### PD-004: Return stable, safe conflict problem details

Retain the current duplicate-email HTTP 409 problem response and code
`CUSTOMER_EMAIL_EXISTS`. Map a stale version to HTTP 409 with a stable public
code such as `CUSTOMER_VERSION_STALE` and a title that explicitly states the
customer version is stale. Do not return database exceptions, the current
stored customer, the supplied version, or provider implementation details.

## Component Changes and Request Flow

### Domain (`CustomerApi.Domain`)

1. Extend `Customer` with `Version` and enforce a positive initial version.
2. Make the updatable profile fields internally mutable (private setters or
   backing fields) while preserving a read-only public API.
3. Add a domain method such as `UpdateProfile(firstName, lastName, normalizedEmail)`.
   It preserves `Id` and `CreatedAt`, changes only profile fields, and increments
   `Version` on every successful update, including a no-op replacement.
4. Preserve constructor validation for identifier and required non-whitespace
   names/email. Application-level email-format validation remains where it is
   today.

### Application (`CustomerApi.Application`)

1. Add an `UpdateCustomer` command, use case, result type, and status enum,
   parallel to `CreateCustomer`.
2. The use case receives the route `Guid`, replacement values, and the supplied
   `long?` version. It returns validation failure without calling persistence
   when the version is missing.
3. It normalizes and validates the profile fields using the same validation
   routine/rules as create. Extract shared validation only if doing so avoids
   duplication without changing observed validation messages.
4. It loads the customer by ID. A missing customer returns the existing
   not-found outcome.
5. Before mutation, it checks whether a customer other than the target owns the
   normalized email. A match returns duplicate-email; the repository still
   handles a unique-index race.
6. It compares the supplied version with the loaded customer version. A mismatch
   returns the concurrency-conflict outcome without mutation.
7. For a match, it calls the domain update method and calls the store update
   operation. Translate its updated, duplicate-email, and concurrency-conflict
   outcomes to typed application results.

### Persistence (`CustomerApi.Infrastructure`)

1. Extend `ICustomerStore` with an update operation and add a
   `CustomerStoreUpdateResult` containing `Updated`, `DuplicateEmail`, and
   `ConcurrencyConflict`.
2. In `CustomerRepository.UpdateAsync`, call `SaveChangesAsync`. Map
   `DbUpdateConcurrencyException` to `ConcurrencyConflict`; reuse the existing
   narrow SQLite unique-index detector for `DuplicateEmail`; allow unrelated
   database failures to propagate.
3. Configure the `Version` property as required and a concurrency token in
   `CustomerDbContext`.
4. Add an EF Core migration that adds a non-null integer `Version` column with
   default value `1`, then update the model snapshot. The default gives every
   pre-existing row a usable initial version; new entities explicitly start at
   `1`. Startup already owns `Database.MigrateAsync`, so no migration runner or
   deployment pattern is added.

### API (`CustomerApi`)

1. Register `UpdateCustomerUseCase` in `Program.cs`.
2. Map `PUT /customers/{id}` in `CustomerEndpoints` with
   `.RequireAuthorization(CustomerPolicies.Write)`.
3. Parse `{id}` exactly as the GET endpoint does. A malformed ID returns the
   same `ValidationProblem` HTTP 400 before the use case runs.
4. Bind `UpdateCustomerRequest`, invoke the use case, and map results:

   | Result | HTTP response |
   | --- | --- |
   | Success | 200 with `CustomerResponse`, including its new `version` |
   | Validation failure, including absent version | 400 validation problem |
   | Missing customer | 404 |
   | Duplicate email | 409 existing `CUSTOMER_EMAIL_EXISTS` problem |
   | Stale version / persistence concurrency conflict | 409 safe `CUSTOMER_VERSION_STALE` problem |
   | Unexpected result | Existing generic 500 problem |

5. Keep standard JWT authentication/authorization behavior, middleware order,
   and sensitive-data exclusions unchanged.
6. Generalize the status-409 request outcome classification from the
   duplicate-specific label to a generic conflict label, then update its unit
   and integration telemetry expectations. This accurately covers both email
   and version conflicts while retaining the existing metric tags and avoiding
   request-payload logging.

## API Representation

Customer responses from POST, GET, and PUT will contain the existing fields
plus the additive field below:

```json
{
  "id": "guid",
  "firstName": "John",
  "lastName": "Doe",
  "email": "john@example.com",
  "createdAt": "2026-09-24T12:00:00+00:00",
  "version": 1
}
```

The PUT request uses the existing camel-case JSON convention and includes the
client's previously obtained version:

```json
{
  "firstName": "John",
  "lastName": "Doe",
  "email": "john@example.com",
  "version": 1
}
```

The selected body member is deliberate: the API currently uses JSON request
records and has no established ETag/conditional-request convention. It keeps
the complete replacement input in one representation and is straightforward
to bind and validate in the existing minimal endpoint style.

## Testing and Validation Strategy

### Domain and application unit tests

- Verify a newly created customer starts at version `1`.
- Verify `UpdateProfile` preserves `Id`/`CreatedAt`, changes allowed profile
  values, and increments version for every successful call.
- Verify successful update normalizes email and returns the advanced version.
- Verify missing version, validation failure, duplicate email, missing customer,
  and stale supplied version do not write or mutate the stored customer.
- Update the in-memory `ICustomerStore` test double to model typed update
  outcomes and version comparison.

### Repository and migration integration tests

- Verify the new migration applies, the snapshot has no pending model changes,
  and existing migrated rows have version `1`.
- Use two independent `DbContext`/repository instances loaded at the same
  version. Save the first update and assert the second returns
  `ConcurrencyConflict`, leaves the first update intact, and does not leak a
  provider exception.
- Preserve and extend duplicate-email race tests to confirm a database unique
  constraint during update maps to `DuplicateEmail`, not concurrency conflict.

### Endpoint integration tests

- Create a customer, read its version, PUT a valid full replacement using that
  version, assert 200, normalized email, stable ID/creation time, and a changed
  response version; GET must return the same updated representation.
- PUT without `version`: assert 400 and no mutation.
- PUT with a stale version: assert 409, `CUSTOMER_VERSION_STALE`, safe response
  body, and no mutation. Retry after a successful update using the old version
  to prove it is unusable.
- Cover missing fields, whitespace names, invalid email, duplicate normalized
  email, missing customer, and malformed GUID using PUT.
- Cover unauthenticated, invalid-token, and authenticated-but-forbidden PUT
  requests; assert no mutation or customer disclosure.
- Update response DTOs and existing POST/GET assertions to include `version`.
  Preserve all existing POST/GET status, authorization, and field assertions.
- Update outcome-classifier and telemetry tests for the generic 409 conflict
  classification; assert telemetry still excludes emails and bearer tokens.

## Requirement Traceability

| Specification coverage | Plan coverage |
| --- | --- |
| FR-001–FR-013 | API mapping and update request flow |
| FR-014–FR-018; BR-011–BR-013 | PD-001 through PD-004; domain, application, and persistence changes |
| BR-001–BR-010 | Shared create/update validation, normalization, unique-index handling, and authorization |
| NFR-001–NFR-005 | Testing strategy, safe problem details, JWT reuse, and telemetry handling |
| EC-001–EC-013; AC-001–AC-010 | Endpoint, repository, migration, application, and domain test coverage |

## Risks and Mitigations

- **SQLite write contention:** EF's concurrency token detects a stale tracked
  write deterministically. Repository integration tests use independent
  contexts against SQLite rather than an in-memory provider.
- **Email and version races:** map only the known email unique-index violation
  to duplicate email; map EF's concurrency exception separately; do not expose
  either provider exception.
- **Migration compatibility:** the non-null default of `1` makes deployed rows
  immediately readable/updatable. Validate with the existing migration/startup
  integration path.
- **Response compatibility:** `version` is additive. Keep all established
  response fields, error formats, policy names, and endpoints unchanged.
