# Customer Update Implementation Tasks

## Conventions

- Execute tasks in numeric order unless all listed dependencies are complete.
- Do not introduce an architectural layer, ETag handling, or a separate
  concurrency service. Extend the existing minimal API, application use-case,
  domain entity, `ICustomerStore`, EF Core repository, and test fixture.
- Requirement references use the approved `spec.md`; design decisions use
  `plan.md` PD identifiers.
- The expected version mechanism is the planned JSON `long` `version` value;
  it is a value clients round-trip, not one they derive.

## T001 — Extend the Customer aggregate with versioned profile updates (COMPLETED)

- **Description:** Add a positive `long Version` to `Customer`, initialize new
  instances at version `1`, and add a domain operation to replace first name,
  last name, and normalized email. Preserve `Id` and `CreatedAt`, keep public
  properties read-only, and increment `Version` on every successful profile
  update, including a no-op replacement.
- **Files:** `src/CustomerApi.Domain/Customer.cs`,
  `tests/CustomerApi.Domain.Tests/CustomerTests.cs`; update direct `Customer`
  construction call sites as needed.
- **Dependencies:** None.
- **Requirement references:** FR-003, FR-004, FR-017, FR-018; BR-001–BR-004,
  BR-012–BR-013; EC-013; AC-001, AC-010; PD-001, PD-002.
- **Done when:** Domain tests prove initial version `1`, profile update preserves
  identity/creation time, changes permitted fields, increments version, and
  preserves existing required-name invariants.

## T002 — Add EF Core version mapping and schema migration (COMPLETED)

- **Description:** Map `Customer.Version` as a required EF Core concurrency
  token. Generate a migration that adds non-null integer `Version` to
  `Customers` with default `1`, and update the EF model snapshot. Retain the
  existing unique normalized-email index and startup migration ownership.
- **Files:** `src/CustomerApi.Infrastructure/Persistence/CustomerDbContext.cs`,
  `src/CustomerApi.Infrastructure/Migrations/<timestamp>_AddCustomerVersion.cs`,
  `src/CustomerApi.Infrastructure/Migrations/CustomerDbContextModelSnapshot.cs`,
  `tests/CustomerApi.IntegrationTests/CustomerRepositoryTests.cs`.
- **Dependencies:** T001.
- **Requirement references:** FR-014, FR-017, FR-018; BR-011–BR-013; NFR-001;
  EC-011, EC-013; PD-001, PD-002.
- **Done when:** The new migration applies to a clean SQLite database, a
  migrated existing row has version `1`, `HasPendingModelChanges()` is false,
  and the model mapping marks `Version` as a concurrency token.

## T003 — Extend the customer store for typed update outcomes (COMPLETED)

- **Description:** Add an update operation to `ICustomerStore` and a typed
  `CustomerStoreUpdateResult` for `Updated`, `DuplicateEmail`, and
  `ConcurrencyConflict`. Update all in-memory test doubles to implement the
  expanded interface.
- **Files:** `src/CustomerApi.Application/Abstractions/ICustomerStore.cs`,
  `tests/CustomerApi.Tests/CustomerUseCaseTests.cs`, and any other
  `ICustomerStore` implementations.
- **Dependencies:** T001.
- **Requirement references:** FR-008, FR-009, FR-014, FR-016; BR-007–BR-009,
  BR-011–BR-012; PD-002.
- **Done when:** The solution compiles with the expanded port and test doubles
  can deterministically return each update outcome.

## T004 — Implement repository update and persistence conflict translation (COMPLETED)

- **Description:** Implement `CustomerRepository.UpdateAsync` using the tracked
  entity and `SaveChangesAsync`. Translate `DbUpdateConcurrencyException` to
  `ConcurrencyConflict`, reuse the existing narrow SQLite email-index detector
  for `DuplicateEmail`, and allow unrelated database errors to propagate. Clear
  or detach failed tracked state so it cannot be persisted later.
- **Files:** `src/CustomerApi.Infrastructure/Persistence/CustomerRepository.cs`,
  `tests/CustomerApi.IntegrationTests/CustomerRepositoryTests.cs`.
- **Dependencies:** T002, T003.
- **Requirement references:** FR-004, FR-008, FR-009, FR-014, FR-016–FR-017;
  BR-007–BR-009, BR-011–BR-013; NFR-002; EC-007, EC-011, EC-013; AC-003,
  AC-008, AC-010; PD-002, PD-004.
- **Done when:** Repository integration tests using independent SQLite contexts
  prove that the first same-version update succeeds, the second returns
  `ConcurrencyConflict` without overwriting it, an email-index race returns
  `DuplicateEmail`, and non-email database failures are not mislabeled.

## T005 — Add the UpdateCustomer application workflow (COMPLETED)

- **Description:** Create `UpdateCustomerCommand`, `UpdateCustomerUseCase`,
  `UpdateCustomerResult`, and status enum following the existing create use
  case. Validate absent version before persistence; normalize and validate the
  replacement fields with create-equivalent rules; return not-found, duplicate,
  or stale-version outcomes without mutation; exclude the target customer from
  the duplicate-email precheck; call the aggregate update and store update only
  after all preconditions match.
- **Files:** `src/CustomerApi.Application/UpdateCustomer/UpdateCustomerCommand.cs`,
  `UpdateCustomerUseCase.cs`, `UpdateCustomerResult.cs`; optionally a shared
  validation helper in `CustomerApi.Application`; `tests/CustomerApi.Tests/CustomerUseCaseTests.cs`.
- **Dependencies:** T001, T003, T004.
- **Requirement references:** FR-002, FR-004–FR-009, FR-014–FR-017;
  BR-001–BR-009, BR-011–BR-013; EC-001–EC-007, EC-011–EC-013; AC-001–AC-004,
  AC-008–AC-010; PD-002, PD-003.
- **Done when:** Unit tests cover success and normalized output, missing version,
  missing/whitespace fields, invalid email, target-not-found, same-email
  success, duplicate email, supplied stale version, and repository-level race
  outcomes, with no write for every rejection.

## T006 — Expose `version` in existing customer responses (COMPLETED)

- **Description:** Add `Version` to `CustomerResponse` and its factory mapping.
  Update create/retrieve unit and integration DTO assertions so POST and GET
  retain all established behavior while returning version `1` for new records.
- **Files:** `src/CustomerApi/Endpoints/CustomerEndpoints.cs`,
  `tests/CustomerApi.IntegrationTests/CustomersEndpointTests.cs`,
  `tests/CustomerApi.Tests/CustomerUseCaseTests.cs` as applicable.
- **Dependencies:** T001.
- **Requirement references:** FR-011, FR-018; NFR-001; AC-001, AC-007;
  PD-001, PD-003.
- **Done when:** Existing POST and GET integration tests pass with the additive
  `version` member and verify that a newly created customer exposes version `1`.

## T007 — Add the secured PUT endpoint and HTTP result mapping (COMPLETED)

- **Description:** Register `UpdateCustomerUseCase` in DI. Add
  `UpdateCustomerRequest` with nullable `firstName`, `lastName`, `email`, and
  `version`; map `PUT /customers/{id}` with `CustomerPolicies.Write`; reuse GET
  GUID parsing and validation-problem behavior. Map successful update to 200
  with `CustomerResponse`, missing version/validation failures to 400, not found
  to 404, duplicate email to the existing `CUSTOMER_EMAIL_EXISTS` 409 problem,
  and stale versions to a safe 409 `CUSTOMER_VERSION_STALE` problem.
- **Files:** `src/CustomerApi/Endpoints/CustomerEndpoints.cs`,
  `src/CustomerApi/Program.cs`,
  `tests/CustomerApi.IntegrationTests/CustomersEndpointTests.cs`.
- **Dependencies:** T005, T006.
- **Requirement references:** FR-001–FR-016, FR-018; BR-001–BR-012;
  NFR-002–NFR-003; EC-001–EC-012; AC-001–AC-009; PD-003, PD-004.
- **Done when:** Endpoint tests prove the complete PUT contract and all result
  mappings, require `Customer.Write`, and confirm malformed IDs are 400 before
  store access. Error responses contain no database/provider details or
  protected customer data.

## T008 — Complete endpoint authorization and update behavior coverage (COMPLETED)

- **Description:** Expand endpoint integration coverage for all approved update
  scenarios against the real JWT pipeline and SQLite database. Include the
  no-mutation assertions required for validation, duplicate, not-found,
  missing-version, stale-version, unauthenticated, invalid-token, and forbidden
  requests.
- **Files:** `tests/CustomerApi.IntegrationTests/CustomersEndpointTests.cs`,
  `tests/CustomerApi.IntegrationTests/CustomerApiFactory.cs` only if fixture
  helpers are required.
- **Dependencies:** T002, T004, T006, T007.
- **Requirement references:** FR-001–FR-018; BR-001–BR-013; NFR-001–NFR-005;
  EC-001–EC-013; AC-001–AC-010.
- **Done when:** Integration tests demonstrate every AC-001–AC-010 clause,
  including stale error code/title safety, version advancement after success,
  rejected old-version reuse, both `scp` and `roles` authorization behavior,
  and create/retrieve backward compatibility.

## T009 — Generalize 409 observability without sensitive data (COMPLETED)

- **Description:** Replace the duplicate-specific status-409 outcome label with
  a generic conflict label in the request outcome classifier, then update
  classifier and telemetry integration assertions. Preserve endpoint/status
  tags and ensure logs/metrics never include email, version values, bearer
  tokens, authorization headers, or request payloads.
- **Files:** `src/CustomerApi/RequestOutcomeClassifier.cs`,
  `tests/CustomerApi.Tests/RequestOutcomeClassifierTests.cs`,
  `tests/CustomerApi.IntegrationTests/CustomersEndpointTests.cs`.
- **Dependencies:** T007.
- **Requirement references:** NFR-002, NFR-005; PD-004; plan Reliability and
  Observability.
- **Done when:** Unit and integration tests verify generic conflict telemetry
  for both duplicate-email and stale-version 409 responses and continue to
  prove sensitive values are absent.

## T010 — Run full verification and record completion evidence (COMPLETED)

- **Description:** Run restore, build, and all solution tests after the update
  migration and endpoint changes. Confirm the migration applies during test
  startup, the model snapshot has no drift, and no existing create/retrieve or
  readiness behavior regresses.
- **Files:** No production file change expected; update
  `specs/002-customer-update/tasks.md` only if implementation uncovers a
  documented remediation item.
- **Dependencies:** T001–T009.
- **Requirement references:** FR-011; NFR-001–NFR-005; AC-007; plan Testing and
  Validation Strategy.
- **Done when:** `dotnet build CustomerDemo.slnx` and `dotnet test
  CustomerDemo.slnx` succeed, migration-snapshot verification passes, and the
  implemented evidence covers AC-001–AC-010.

## Execution Order and Critical Dependencies

1. Complete **T001** before any version-aware logic.
2. **T002** and **T003** can proceed after T001; both are required by the
   persistence implementation in **T004**.
3. **T005** depends on the aggregate, port, and repository outcomes.
4. **T006** can run after T001 and should finish before the PUT contract in
   **T007**, because update responses reuse `CustomerResponse`.
5. **T008** validates the assembled persistence and API flow. **T009** follows
   endpoint completion because it verifies real 409 telemetry.
6. **T010** is the release gate; do not mark the feature complete if any
   migration, build, or test verification fails.

## Traceability Summary

| Requirement group | Implementation tasks | Verification tasks |
| --- | --- | --- |
| FR-001–FR-013; BR-001–BR-010 | T005–T007 | T005, T007, T008, T010 |
| FR-014–FR-018; BR-011–BR-013 | T001–T007 | T001, T002, T004, T005, T008 |
| NFR-001–NFR-005 | T002, T007, T009 | T004, T008–T010 |
| EC-001–EC-013; AC-001–AC-010 | T001–T009 | T004, T005, T007, T008, T010 |

## Convergence Remediation Tasks

### RT001 — Verify migration of pre-existing customers to version 1 (COMPLETED)

- **Problem:** The migration and clean-database snapshot test pass, but no test
  applies `20260929000000_AddCustomerVersion` to a database containing a
  customer created under the initial schema. The readiness test also asserts
  only the initial migration ID. This leaves the plan's deployed-data migration
  behavior unverified.
- **Affected requirements:** FR-017, FR-018; BR-012–BR-013; NFR-001; PD-001,
  PD-002; T002 done criteria and plan Migration Compatibility risk.
- **Required change:** Add an integration test that migrates only through the
  initial migration, inserts a customer, applies the version migration, and
  verifies the existing row has `Version == 1`, remains readable, and can be
  updated with optimistic concurrency. Strengthen startup/readiness migration
  evidence to assert the version migration is applied.
- **Validation criteria:** The pre-existing row receives version `1` without
  data loss; an update using version `1` succeeds and advances the version; all
  migration, build, and solution tests pass with no pending model changes.
- **Dependencies:** T002, T004, T010.

### RT002 — Complete version advancement and stale-error safety evidence (COMPLETED)

- **Problem:** Production code increments the version for a no-op replacement,
  but no automated test proves the plan's explicit no-op behavior. Stale-version
  endpoint tests check the public code/title but do not explicitly prove that
  current/supplied versions, customer data, provider exceptions, or internal
  implementation details are absent from the response. Telemetry tests likewise
  do not explicitly assert that version values or request payloads are omitted.
- **Affected requirements:** FR-016–FR-017; BR-011–BR-013; NFR-002, NFR-004,
  NFR-005; EC-011, EC-013; AC-008, AC-010; PD-001, PD-004; T001 and T009 done
  criteria.
- **Required change:** Add domain/application or endpoint tests for a successful
  no-op replacement that advances the version and makes the prior version stale.
  Strengthen stale-conflict response and telemetry assertions to exclude version
  values, profile data, request payloads, exception/provider names, and other
  internal details while retaining the stable public code/title.
- **Validation criteria:** Tests prove every successful replacement, including a
  no-op, advances the version; reuse of the old version returns the safe 409 and
  leaves persistence unchanged; response/log/metric assertions demonstrate the
  required non-disclosure; all tests pass.
- **Dependencies:** T001, T007–T010.

### RT003 — Strengthen rejection atomicity and update authorization disclosure tests (COMPLETED)

- **Problem:** The duplicate-email endpoint test verifies only the target
  customer remains unchanged, not both customers as required by AC-003/EC-007.
  Update-specific 401/403 tests verify status and persistence but do not assert
  that response bodies omit the protected customer ID, email, and attempted
  profile data as required by EC-009 and the security guardrails. T008 also
  claims both `scp` and `roles` authorization behavior, but successful update is
  exercised only with an `scp` `Customer.Write` claim.
- **Affected requirements:** FR-008–FR-010; BR-008, BR-010; NFR-001–NFR-003;
  EC-007, EC-009; AC-003, AC-005–AC-007.
- **Required change:** Extend endpoint integration tests to retrieve and compare
  both customers after duplicate-email rejection. For unauthenticated,
  invalid-token, and authenticated-but-forbidden PUT requests, assert response
  bodies disclose neither stored customer identity/profile values nor attempted
  update values, while retaining the existing no-mutation assertions. Add a
  successful PUT authorized by a `roles` `Customer.Write` claim.
- **Validation criteria:** Tests prove both duplicate-conflict records are
  byte-for-byte semantically unchanged and all three update authorization
  rejection modes return the established statuses without protected-data
  disclosure; both supported permission claim types authorize `Customer.Write`;
  all solution tests pass.
- **Dependencies:** T007, T008, T010.

### RT004 — Resolve specification approval status (COMPLETED)

- **Problem:** `spec.md` declares `Specification status: DRAFT`, while the
  shared contract requires an approved specification before Plan, Tasks,
  Implement, and Converge handoffs. Downstream work cannot silently approve or
  rewrite this upstream-owned status.
- **Affected requirements:** Shared contract Specify → Plan handoff and
  Definition of Done; all FR, BR, NFR, EC, and AC traceability for feature
  `002-customer-update`.
- **Required change:** The specification owner must review the resolved feature
  requirements and explicitly record the approved status, or document why the
  downstream artifacts must be withdrawn/reworked if approval is not granted.
- **Validation criteria:** `spec.md` carries an explicit owner-approved status
  with requirement IDs preserved, and Plan/Tasks remain consistent with the
  approved specification.
- **Dependencies:** Specification-owner approval; precedes final convergence.

### RT005 — Repair deterministic convergence evidence failures (COMPLETED)

- **Problem:** Final convergence verification fails in two remediation tests.
  `VersionMigration_ExistingCustomerGetsVersionOne_AndCanBeUpdated` inserts a
  legacy GUID using a raw SQLite representation that EF Core does not retrieve
  by the same `Guid`, so the migrated row assertion fails. The telemetry test
  rejects any log containing the JSON token name `"version"`, but this also
  matches the legitimate migration name `AddCustomerVersion`, causing a false
  positive before it can prove payload non-disclosure.
- **Affected requirements:** NFR-001, NFR-005; RT001 and RT002 validation
  criteria; T002, T009, and T010 done criteria.
- **Required change:** Make the legacy-row setup use the same SQLite value
  conversion as the initial EF model while still inserting under the initial
  schema. Scope telemetry non-disclosure assertions to actual request payload
  or sensitive version values without rejecting migration metadata. Preserve
  the intent and strength of both tests.
- **Validation criteria:** Both affected tests pass independently and in the
  full solution run; `dotnet build CustomerDemo.slnx` succeeds; `dotnet test
  CustomerDemo.slnx` reports zero failures; migration evidence still proves a
  pre-existing row receives version `1` and can update to version `2`; telemetry
  evidence still proves request payloads and version values are not disclosed.
- **Dependencies:** RT001, RT002, RT004, T010.
