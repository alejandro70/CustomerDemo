# Customer API Technical Plan

## Scope and Context

Feature `001-customer` introduces authenticated API operations to create one
customer and retrieve one customer by identifier. The repository currently
contains the Spec Kit artifacts only, so this plan establishes the initial
application structure without changing the approved requirements.

## Architecture

Implement a single ASP.NET Core Web API service with explicit presentation,
application, domain, and infrastructure boundaries:

- **API:** Customer endpoints, request/response contracts, model validation,
  authorization policies, and RFC 7807-style error responses.
- **Application:** `CreateCustomer` and `GetCustomerById` use cases. They own
  orchestration, validation invocation, normalization, and error translation.
- **Domain:** Customer aggregate/value rules for required non-whitespace names
  and normalized email. It owns no HTTP or database types.
- **Infrastructure:** EF Core customer repository, relational schema and
  migrations, UTC clock/identifier providers, and Microsoft Entra ID JWT
  Bearer configuration.

The API project composes dependencies through the built-in ASP.NET Core DI
container. Interfaces are introduced only at the application-to-infrastructure
boundary for the customer store, clock, and identifier generation so business
behavior remains testable without HTTP or a database.

### PD-001: Use ASP.NET Core Web API with EF Core and a relational store

Use ASP.NET Core's minimal APIs or controllers consistently across the new
service, EF Core for persistence, and a configured relational database
provider. EF Core migrations create and evolve the schema. A relational unique
index enforces the email invariant under concurrent requests; application-level
checks alone are insufficient.

Supports FR-001 through FR-010, BR-005 through BR-009, and NFR-001.

### PD-002: Expose GUID customer identifiers

Generate a GUID for each successful creation and expose it as the API `Id`.
The retrieval route parses the path value as a GUID before executing the use
case; parsing failure produces HTTP 400. Valid but absent GUIDs reach the
lookup and produce HTTP 404.

Supports FR-002, FR-004 through FR-006, FR-010, EC-006, EC-010, AC-001 through
AC-003, and AC-011.

### PD-003: Persist normalized email as the unique value

The creation use case trims the submitted email and converts it to lowercase
using invariant casing before validation, comparison, and storage. The
database column stores that normalized value and has a unique index. Names are
validated after checking for null, empty, or whitespace-only values; their
submitted content is otherwise preserved.

Supports FR-007 and FR-009, BR-001 through BR-006 and BR-008 through BR-010,
EC-001 through EC-005 and EC-008 through EC-011, and AC-004, AC-005, and
AC-008 through AC-010.

### PD-004: Authenticate with Entra JWT Bearer and authorize per operation

Configure ASP.NET Core JWT Bearer authentication from Microsoft Entra ID
tenant, client/application ID, and authority/audience settings supplied by
configuration or secret stores. Define `Customer.Write` and `Customer.Read`
authorization policies. Each policy requires the corresponding permission in
the Entra token's delegated `scp` claim or application `roles` claim, allowing
both supported OAuth client models while never accepting an unrelated claim.
Apply the write policy to creation and the read policy to retrieval.

Supports FR-008, FR-011, FR-012, BR-007, EC-007, EC-012, AC-007, AC-012,
AC-013, and NFR-003.

## Components and Request Flow

### Create Customer

1. `POST /customers` receives a create request with `firstName`, `lastName`,
   and `email` and requires the `Customer.Write` policy.
2. API binding and application validation reject missing or whitespace-only
   names and missing/invalid normalized email with HTTP 400. Error details are
   limited to field-level validation messages.
3. The application checks the normalized email for an existing customer.
4. If found, return HTTP 409 with a stable error code
   `CUSTOMER_EMAIL_EXISTS` and do not persist a record.
5. For a new email, generate the GUID and UTC `CreatedAt`, persist exactly one
   customer, and return HTTP 201 with the customer representation. Include a
   `Location` header for the retrieval resource when the selected API style
   supports it.
6. If a simultaneous insert violates the database unique index, translate the
   provider-specific uniqueness exception to the same HTTP 409 response.

### Retrieve Customer

1. `GET /customers/{id}` requires the `Customer.Read` policy.
2. Parse `{id}` as a GUID; malformed values return HTTP 400 without repository
   access.
3. Retrieve the customer by identifier. Return HTTP 404 when absent, otherwise
   return HTTP 200 with the complete customer representation.

## Data and Persistence

Create a `Customers` table/entity with:

| Column | Type/constraint | Purpose |
| --- | --- | --- |
| `Id` | GUID primary key, non-null | API customer identifier |
| `FirstName` | required text | Submitted identifying value |
| `LastName` | required text | Submitted identifying value |
| `Email` | required normalized text, unique index | Contact value and uniqueness key |
| `CreatedAt` | non-null UTC timestamp | Server-assigned creation time |

The first EF Core migration creates the table and unique index. It must be
applied through the deployment migration process before the service accepts
traffic. No data migration is needed because this is a new feature.

## API Contract and Errors

The public response representation has `id`, `firstName`, `lastName`, `email`,
and `createdAt`. Creation accepts only the three client-supplied fields;
server-assigned fields are never accepted as authoritative input.

Use a consistent problem-details response shape for failures:

| Condition | Status | Required public detail |
| --- | --- | --- |
| Invalid creation data or malformed ID | 400 | Safe validation/problem details |
| Missing customer | 404 | Resource not found |
| Normalized email already exists | 409 | `CUSTOMER_EMAIL_EXISTS` error code |
| No/invalid JWT | 401 | Standard authentication challenge; no customer data |
| Valid JWT without operation permission | 403 | Authorization failure; no customer data |
| Unexpected persistence/service error | 500 | Generic problem details only |

### PD-005: Map uniqueness conflicts at the infrastructure boundary

The repository/application boundary exposes a domain-specific duplicate-email
outcome. Both the pre-insert lookup and a database unique-constraint violation
map to it, so the HTTP layer emits the required 409 response without leaking a
database provider exception.

Supports FR-009, BR-005 through BR-006, EC-005 and EC-008, AC-005 and AC-008,
and NFR-002.

## Security and Configuration

Configuration provides the connection string and Entra settings using standard
ASP.NET Core configuration precedence. Production secrets come from deployment
environment/secret management, never source control or logs. Validate required
settings during startup and fail fast with non-secret diagnostics when absent or
invalid.

Enable HTTPS, JWT issuer/audience/signature/lifetime validation, and default
authentication/authorization middleware in the correct pipeline order. Do not
log bearer tokens, authorization headers, or full sensitive request payloads.

## Reliability and Observability

The database unique index is the concurrency authority for email uniqueness.
Handle expected uniqueness races deterministically as 409; let transient
database failures use the configured provider resilience policy where
available, and expose only generic 500 responses once retries are exhausted.

Emit structured logs and metrics for request outcome, endpoint, status code,
validation rejection, duplicate-email conflict, authorization result, and
unhandled failure. Correlate logs with the request trace identifier. Exclude
email values and token material from normal logs.

## Testing and Validation Strategy

- **Domain/unit tests:** normalization, required/whitespace-only names, required
  email, email-format validation using the standard ASP.NET Core policy, and
  server-generated identifier/UTC creation time behavior.
- **Application/unit tests:** successful create, duplicate detection, no write
  on validation or duplicate failure, successful retrieval, and missing
  customer outcome.
- **API integration tests:** HTTP status codes and bodies for AC-001 through
  AC-005 and AC-008 through AC-011, including normalized returned email and
  invalid GUID handling. Use an isolated relational test database so the unique
  constraint and race translation are exercised.
- **Authentication/authorization integration tests:** Entra JWT Bearer
  configuration validation plus authenticated test tokens for both accepted
  permission claim shapes. Verify 401 for absent/invalid tokens and 403 for
  valid tokens missing `Customer.Write` or `Customer.Read`, with no data
  disclosure or mutation.
- **Migration verification:** apply the initial migration to a clean database
  in CI and assert the unique email index exists through behavioral duplicate
  tests.

## Requirement Traceability

| Requirement | Design coverage |
| --- | --- |
| FR-001 to FR-003 | Create Customer flow, API contract, PD-001/PD-003 |
| FR-004 to FR-006 | Retrieve Customer flow, PD-002 |
| FR-007, BR-001 to BR-004, BR-010 | Create validation and PD-003 |
| FR-008, FR-011, FR-012, BR-007 | PD-004, Security and Configuration |
| FR-009, BR-005, BR-006, BR-008, BR-009 | PD-001, PD-003, PD-005, persistence unique index |
| FR-010 | PD-002 and Retrieve Customer flow |
| NFR-001, NFR-003 | Testing and Validation Strategy |
| NFR-002 | API errors, PD-005, Security and Configuration |
| EC-001 to EC-012, AC-001 to AC-013 | Request flows, API errors, and Testing and Validation Strategy |

## Deployment, Risks, and Trade-offs

Deploy the migration before or as an ordered release step ahead of application
instances using the new schema. Supply Entra and database settings through the
target environment's configuration system. Health checks should distinguish
process readiness from database dependency readiness without exposing secrets.

Primary risks are incorrect Entra audience/permission claim mapping, a
database provider-specific duplicate-key exception not translated to 409, and
email validation behavior drifting from ASP.NET Core's standard policy. Mitigate
them with startup configuration validation, provider-backed integration tests,
and tests that invoke the same validation mechanism configured by the API.

The relational store adds operational setup compared with an in-memory store,
but is required to preserve email uniqueness across service instances and
concurrent requests.