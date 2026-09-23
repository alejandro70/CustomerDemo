# Customer API Implementation Tasks

## Task Conventions

- Implement tasks in numeric order unless their dependencies have already been satisfied.
- Requirement references include functional requirements (FR), business rules (BR), non-functional requirements (NFR), edge cases (EC), and acceptance criteria (AC).
- Target paths establish the initial solution layout; retain equivalent existing paths if the implementation scaffold uses a different convention.

## T001 - Scaffold the API solution and project boundaries

- **Description:** Create the ASP.NET Core Web API solution and projects for API, Application, Domain, Infrastructure, and their test suites. Establish references so API composes Application and Infrastructure, Application depends on Domain abstractions, and Infrastructure implements Application ports.
- **Requirement references:** FR-001 through FR-012; NFR-001 through NFR-003; PD-001.
- **Target component/file:** `src/CustomerApi.Api/`, `src/CustomerApi.Application/`, `src/CustomerApi.Domain/`, `src/CustomerApi.Infrastructure/`, `tests/CustomerApi.*.Tests/`, solution and project files.
- **Dependencies:** None.
- **Expected result:** A buildable solution with explicit presentation, application, domain, and infrastructure boundaries and test projects ready for unit and integration coverage.
- **Validation criteria:** Restore and build the solution successfully; verify prohibited dependency directions are absent from project references.

## T002 - Define the customer domain model and application ports

- **Description:** Implement the `Customer` domain entity and application-facing store, clock, and identifier-generator interfaces. Model server-assigned GUID `Id` and UTC `CreatedAt`; preserve submitted name content while enforcing required, non-whitespace names. Define normalized-email handling and duplicate/not-found outcomes without HTTP or EF Core dependencies.
- **Requirement references:** FR-001, FR-002, FR-004 through FR-007, FR-009, FR-010; BR-001 through BR-006, BR-008 through BR-010; EC-001 through EC-006, EC-008 through EC-011; PD-002, PD-003, PD-005.
- **Target component/file:** `src/CustomerApi.Domain/Customer.cs`, `src/CustomerApi.Application/Abstractions/ICustomerStore.cs`, `IClock.cs`, `IIdentifierGenerator.cs`, customer outcome/result types.
- **Dependencies:** T001.
- **Expected result:** The application has dependency-inverted contracts for persisting and retrieving customers and deterministic domain rules for valid customer state.
- **Validation criteria:** Domain unit tests prove missing and whitespace-only names are rejected, email normalization trims and uses invariant lowercase, and a created entity retains its GUID and UTC timestamp.

## T003 - Implement creation and retrieval use cases with validation

- **Description:** Implement `CreateCustomer` and `GetCustomerById` use cases. Normalize email before standard ASP.NET Core email validation, lookup, and persistence; reject invalid input before any write; generate the identifier and UTC creation time only for valid new customers; translate duplicate outcomes to a domain-specific conflict; retrieve valid GUIDs and report absence separately.
- **Requirement references:** FR-001, FR-002, FR-004 through FR-007, FR-009, FR-010; BR-001 through BR-006, BR-008 through BR-010; EC-001 through EC-006, EC-008 through EC-011; AC-001 through AC-005, AC-008 through AC-011; PD-002, PD-003, PD-005.
- **Target component/file:** `src/CustomerApi.Application/CreateCustomer/`, `src/CustomerApi.Application/GetCustomerById/`, validators and result types.
- **Dependencies:** T002.
- **Expected result:** Use cases return explicit success, validation, duplicate-email, and not-found results, while invalid and duplicate operations never write a record.
- **Validation criteria:** Application unit tests cover successful creation, returned normalized email, no write for every invalid input, case-insensitive duplicate detection, no write for a duplicate, successful lookup, and missing-customer outcome.

## T004 - Implement EF Core persistence and the initial migration

- **Description:** Add the EF Core database context, customer entity mapping, repository implementation, UTC/GUID providers, and first migration. Create a non-null `Customers` table with GUID primary key, required names, normalized email, UTC timestamp, and a unique email index. Translate provider-specific unique-constraint exceptions into the duplicate-email application outcome.
- **Requirement references:** FR-002, FR-005, FR-006, FR-009; BR-005, BR-006, BR-008, BR-009; EC-005, EC-008, EC-011; AC-001 through AC-005, AC-008, AC-009; NFR-001, NFR-002; PD-001, PD-002, PD-003, PD-005.
- **Target component/file:** `src/CustomerApi.Infrastructure/Persistence/CustomerDbContext.cs`, `CustomerRepository.cs`, entity configurations, provider implementations, `Migrations/`.
- **Dependencies:** T002, T003.
- **Expected result:** A relational database persists normalized customers, enforces email uniqueness across concurrent writers, and surfaces duplicate conflicts without provider detail leakage.
- **Validation criteria:** Apply the migration to a clean relational test database; verify the schema and unique index; repository/integration tests confirm a duplicate insert maps to the duplicate outcome and preserves the original record.

## T005 - Configure Entra JWT authentication and operation authorization

- **Description:** Bind and validate database and Microsoft Entra ID configuration at startup without logging secrets. Configure HTTPS, JWT Bearer issuer, audience, signature, and lifetime validation. Define `Customer.Write` and `Customer.Read` policies that accept only the matching delegated `scp` permission or application `roles` permission, and register authentication and authorization middleware in correct order.
- **Requirement references:** FR-008, FR-011, FR-012; BR-007; EC-007, EC-012; AC-007, AC-012, AC-013; NFR-002, NFR-003; PD-004.
- **Target component/file:** `src/CustomerApi.Api/Program.cs`, `Authentication/`, `Authorization/`, `appsettings.json`, environment configuration documentation.
- **Dependencies:** T001.
- **Expected result:** The service fails fast for invalid required non-secret configuration, challenges absent/invalid tokens, and authorizes each operation only for its designated permission.
- **Validation criteria:** Authentication/authorization integration tests exercise Entra-compatible JWT validation, both accepted claim shapes, 401 for missing or invalid tokens, and 403 for authenticated tokens without the requested permission.

## T006 - Expose secured customer HTTP endpoints and problem responses

- **Description:** Add `POST /customers` and `GET /customers/{id}` endpoints using the selected API style consistently. Bind only `firstName`, `lastName`, and `email` for creation; require write/read policy respectively; parse route identifiers as GUIDs before lookup; map use-case outcomes to the specified JSON customer representation and RFC 7807-style safe errors. Return 201 on creation, 200 on retrieval, 400 for validation/malformed IDs, 404 when absent, and 409 with `CUSTOMER_EMAIL_EXISTS` for duplicates; include a retrieval `Location` header on 201 where supported.
- **Requirement references:** FR-001 through FR-012; BR-001 through BR-010; EC-001 through EC-012; AC-001 through AC-013; NFR-002; PD-002 through PD-005.
- **Target component/file:** `src/CustomerApi.Api/Endpoints/Customers*` or `Controllers/CustomersController.cs`, request/response DTOs, problem-details mapping, `Program.cs`.
- **Dependencies:** T003, T004, T005.
- **Expected result:** The public HTTP contract exposes only the approved customer fields, correct statuses, stable duplicate error code, and no customer data for denied requests.
- **Validation criteria:** Endpoint-focused integration tests assert response status, body shape, `Location` header when emitted, stable duplicate error code, and safe problem details that omit provider/internal exception information.

## T007 - Add end-to-end API and authorization integration coverage

- **Description:** Build an isolated relational-database integration test fixture that applies the initial migration and supplies valid Entra-compatible test tokens. Cover all approved customer workflows, error paths, data preservation, normalization, and access control against the running API pipeline.
- **Requirement references:** FR-001 through FR-012; BR-001 through BR-010; NFR-001 through NFR-003; EC-001 through EC-012; AC-001 through AC-013; PD-001 through PD-005.
- **Target component/file:** `tests/CustomerApi.Api.IntegrationTests/CustomersEndpointTests.cs`, authentication test-token support, relational database fixture.
- **Dependencies:** T004, T005, T006.
- **Expected result:** Automated integration evidence proves the observable contract, including actual relational uniqueness behavior and the real authorization pipeline.
- **Validation criteria:** Tests explicitly demonstrate: 201 creation with non-empty ID and timestamp (AC-001); 200 retrieval with identical fields (AC-002); 404 absence (AC-003); 400 for missing/invalid/whitespace fields (AC-004, AC-010); duplicate rejection without a second record (AC-005); normalized conflict 409/error code and normalized success email (AC-008, AC-009); malformed GUID 400 (AC-011); 401 absent/invalid JWT with no disclosure or mutation (AC-012); and 403 missing read/write permission with no disclosure or mutation (AC-013).

## T008 - Add operational safeguards and deployment verification

- **Description:** Add readiness and database health checks, structured outcome telemetry that excludes email values, bearer tokens, authorization headers, and full sensitive request payloads, and deployment configuration for ordered migration execution before service traffic. Document required configuration keys and secret-management expectations without committing secrets.
- **Requirement references:** FR-009, FR-011, FR-012; BR-005 through BR-007; NFR-001 through NFR-003; PD-001, PD-004, PD-005.
- **Target component/file:** `src/CustomerApi.Api/Program.cs`, health-check/telemetry configuration, deployment manifests or pipeline configuration, `README.md` or service operations documentation.
- **Dependencies:** T004, T005, T006.
- **Expected result:** Deployment applies the schema before application instances accept traffic, readiness distinguishes database availability, and routine diagnostics never expose protected data.
- **Validation criteria:** CI or deployment verification applies the migration to an empty database before readiness succeeds; configuration validation is covered by startup tests; log/telemetry tests or inspection confirm token, authorization-header, and email values are excluded; full build and all test suites pass.

## Traceability Summary

| Requirement group | Implementation tasks | Validation task |
| --- | --- | --- |
| FR-001 to FR-003 | T002, T003, T004, T006 | T003, T007 |
| FR-004 to FR-006, FR-010 | T002, T003, T004, T006 | T003, T007 |
| FR-007; BR-001 to BR-004, BR-010 | T002, T003, T006 | T002, T003, T007 |
| FR-008, FR-011, FR-012; BR-007 | T005, T006 | T005, T007 |
| FR-009; BR-005, BR-006, BR-008, BR-009 | T002, T003, T004, T006 | T003, T004, T007 |
| NFR-001 | T001, T007, T008 | T007, T008 |
| NFR-002 | T004, T005, T006, T008 | T006, T008 |
| NFR-003 | T005, T007 | T005, T007 |
| EC-001 to EC-012; AC-001 to AC-013 | T002 through T007 | T007 |
