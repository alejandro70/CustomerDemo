# Customer Update Feature Specification

## Status

- Specification status: APPROVED
- Clarification status: RESOLVED

## Problem

The Customer API currently allows creating and retrieving customers but does not
provide a way to modify an existing customer's profile data. Consumers need to
correct or update first name, last name, and email while preserving the
customer identity.

## Value

Consumers can keep customer records accurate over time without creating
duplicate records or changing customer identity, while retaining the API's
existing validation, uniqueness, authorization, and error-handling behavior.

## Goals

- Update an existing customer's `FirstName`, `LastName`, and `Email`.
- Preserve customer identity by prohibiting any change to `Id`.
- Reuse existing business rules for required names, email normalization,
  email format validation, and email uniqueness.
- Preserve existing authentication and authorization behavior.
- Preserve backward compatibility for current customer create/retrieve behavior.
- Provide automated verification of update behavior and compatibility.

## Non-Goals

- Changing a customer's `Id`.
- Adding customer delete, list, or search capabilities.
- Changing create or retrieve API contracts.
- Introducing a new authentication or authorization model.
- Redesigning project architecture.

## Actors and Scenarios

### Authorized Updater

1. An authorized consumer requests an update for an existing customer.
2. The consumer sends `PUT /customers/{id}` with replacement values for
  `FirstName`, `LastName`, and `Email`, and the required concurrency
  precondition.
3. The API validates and normalizes input using existing rules.
4. When the concurrency precondition matches the current customer version, the
  API applies the update and returns `200 OK` with the updated customer.

### Update With Invalid Data

1. An authorized consumer submits missing, whitespace-only, or invalid values.
2. The API rejects the request according to existing validation behavior.
3. The customer record remains unchanged.

### Update With Duplicate Email

1. A different customer already uses the target normalized email.
2. The consumer attempts to update another customer to that email.
3. The API rejects the request as a uniqueness conflict.
4. The existing records remain unchanged.

### Update Missing Customer

1. A consumer requests an update for a customer identifier with no matching
   record.
2. The API handles the request according to the existing not-found behavior.

### Unauthorized Updater

1. A consumer attempts to update a customer without the required authorization
   context.
2. The API denies access and performs no update.

## Functional Requirements

- **FR-001:** The API shall provide an operation to update an existing customer
  using `PUT /customers/{id}`.
- **FR-002:** The update operation shall use full-resource replacement
  semantics and shall require client-supplied values for `FirstName`,
  `LastName`, and `Email`.
- **FR-003:** The API shall not allow changing customer `Id` through the update
  operation.
- **FR-004:** When the target customer exists and the update request is valid,
  and its concurrency precondition matches, the API shall persist the updated
  `FirstName`, `LastName`, and normalized `Email` for that same customer.
- **FR-005:** When no customer exists for the requested `Id`, the API shall
  apply the existing customer not-found behavior.
- **FR-006:** The API shall reject invalid update data using the same input
  validation behavior used for customer creation.
- **FR-007:** The API shall apply email normalization during update using the
  same normalization behavior used for customer creation before validation,
  comparison, and persistence.
- **FR-008:** The API shall reject an update when the normalized target email is
  already in use by a different customer.
- **FR-009:** When email uniqueness causes update rejection, the API shall use
  the existing duplicate-email conflict behavior.
- **FR-010:** The API shall enforce the existing authentication and
  authorization model for customer update operations using the
  `Customer.Write` permission.
- **FR-011:** Existing customer create and retrieve API behavior shall remain
  backward compatible after introducing customer update.
- **FR-012:** On a successful update, the API shall return `200 OK` and the
  updated customer representation.
- **FR-013:** An invalidly formatted `{id}` value on the update route shall
  return HTTP 400 using the existing customer-identifier validation behavior.
- **FR-014:** The update operation shall use optimistic concurrency and shall
  require the client to supply the version of the Customer it previously
  obtained; it shall not persist an update when that version does not match the
  current Customer version.
- **FR-015:** When an update request does not supply the required Customer
  version, the API shall return HTTP 400 Bad Request and shall not modify the
  Customer.
- **FR-016:** When an update request supplies a Customer version that is stale,
  the API shall return HTTP 409 Conflict, shall not modify the Customer, and
  shall communicate that the conflict is caused by a stale Customer version.
- **FR-017:** Following a successful update, the Customer version shall change
  so the version used for that update cannot successfully be reused for a later
  update.
- **FR-018:** The API shall make the current Customer version available to a
  client so that the client can supply it with a subsequent update request.

## Business Rules

- **BR-001:** `FirstName` is required for customer update.
- **BR-002:** `LastName` is required for customer update.
- **BR-003:** `Email` is required for customer update.
- **BR-004:** `FirstName` and `LastName` values containing only whitespace are
  invalid for customer update.
- **BR-005:** Before update validation, uniqueness comparison, and persistence,
  `Email` shall be trimmed and normalized to lowercase.
- **BR-006:** After normalization, update `Email` must satisfy the same standard
  ASP.NET Core email-address validation policy used by create behavior.
- **BR-007:** Email uniqueness remains case-insensitive after normalization.
- **BR-008:** A rejected update request shall not modify the existing customer
  record.
- **BR-009:** A customer may be updated with its current email value when no
  other customer owns that normalized email.
- **BR-010:** An unauthorized user shall not update customer data.
- **BR-011:** A concurrent-update conflict shall not modify customer data.
- **BR-012:** A Customer version is valid for an update only when it matches
  the current version of that Customer.
- **BR-013:** A Customer version used in a successful update becomes stale when
  that update succeeds.

## Non-Functional Requirements

- **NFR-001:** Automated tests shall verify successful update,
  invalid-update rejection, duplicate-email rejection on update,
  not-found update handling, update authorization enforcement, and backward
  compatibility of existing create/retrieve behavior.
- **NFR-002:** Update validation and conflict responses shall not expose
  internal system details.
- **NFR-003:** Update authorization tests shall cover unauthenticated,
  invalid-token, and authenticated-but-forbidden behaviors using the existing
  JWT Bearer and policy model.
- **NFR-004:** Automated tests shall verify that the update endpoint requires
  `Customer.Write` authorization and that a stale concurrency precondition is
  rejected without modifying the customer.
- **NFR-005:** Automated tests shall verify missing-version rejection, stale-
  version conflict handling, Customer-version change after a successful update,
  and non-disclosure of internal implementation details in concurrency errors.

## Edge Cases

- **EC-001:** An update request with missing `FirstName` is invalid.
- **EC-002:** An update request with missing `LastName` is invalid.
- **EC-003:** An update request with missing `Email` is invalid.
- **EC-004:** An update request with whitespace-only `FirstName` or `LastName`
  is invalid.
- **EC-005:** An update request whose normalized email fails the standard
  ASP.NET Core email validation policy is invalid.
- **EC-006:** An update request using `Email` value
  `  John@Example.COM  ` persists and returns `john@example.com` on success.
- **EC-007:** An update request whose normalized email belongs to a different
  customer is rejected and leaves all customer records unchanged.
- **EC-008:** An update request for a nonexistent customer identifier follows
  the existing not-found behavior.
- **EC-009:** An unauthorized request to update a customer is denied and does
  not disclose protected customer data.
- **EC-010:** An invalidly formatted customer `Id` in an update request is
  rejected with HTTP 400 consistently with existing customer identifier format
  behavior.
- **EC-011:** An update request with a stale concurrency precondition is
  rejected and leaves the existing customer record unchanged.
- **EC-012:** An update request that omits the required Customer version is
  rejected with HTTP 400 and leaves the existing customer record unchanged.
- **EC-013:** A Customer version used for a successful update cannot be reused
  in a subsequent update, because it is stale after the successful update.

## Acceptance Criteria

- **AC-001:** Given an existing customer and valid update values for first
  name, last name, and email and a matching Customer version, when an
  authorized consumer sends `PUT /customers/{id}`, then the API returns
  `200 OK` with the updated customer and subsequent retrieval returns the
  updated first name, last name, and normalized email for the same `Id`.
- **AC-002:** Given an update request with missing first name, missing last
  name, missing email, whitespace-only names, or normalized email that fails
  the standard ASP.NET Core email validation policy, when submitted, then the
  API rejects the request according to existing validation behavior and no
  customer data is modified.
- **AC-003:** Given customer A exists with email `john@example.com`, when an
  authorized consumer attempts to update customer B to email
  `  John@Example.COM  `, then the API rejects the request using the existing
  duplicate-email conflict behavior and both customer records remain unchanged.
- **AC-004:** Given no customer exists for an update identifier, when an
  authorized consumer submits an update, then the API responds using the
  existing not-found behavior.
- **AC-005:** Given a request without a valid JWT Bearer access token, when a
  consumer attempts a customer update, then the API responds with the existing
  unauthenticated behavior and no customer data is modified.
- **AC-006:** Given an authenticated request that does not satisfy the required
  update authorization policy, when a consumer attempts a customer update, then
  the API responds with the existing forbidden behavior and no customer data is
  modified.
- **AC-007:** Automated tests demonstrate AC-001 through AC-006 and confirm
  existing create and retrieve behaviors remain backward compatible.
- **AC-008:** Given an update request whose concurrency precondition is stale,
  when an authorized consumer submits it, then the API returns HTTP 409
  Conflict, communicates that the Customer version is stale, and the customer
  remains unchanged.
- **AC-009:** Given an update request that omits the required Customer version,
  when an authorized consumer submits it, then the API returns HTTP 400 Bad
  Request and the customer remains unchanged.
- **AC-010:** Given a successful update using Customer version A, when the
  consumer submits a later update using version A, then the API returns HTTP
  409 Conflict and the customer remains unchanged.

## Assumptions

- Customer identity is represented by the existing `Id` model and remains the
  stable key for update targeting.
- Existing customer create/retrieve endpoints and semantics remain unchanged.
- Existing normalization and duplicate-email conventions are reused for update
  operations.

## Decisions

### Resolved Decisions

- **OQ-001 (RESOLVED):** The public update API is `PUT /customers/{id}` with
  full-resource replacement semantics.
- **OQ-002 (RESOLVED):** A successful update returns `200 OK` with the updated
  customer representation.
- **OQ-003 (RESOLVED):** An invalidly formatted update `Id` returns HTTP 400,
  consistent with retrieval.
- **OQ-004 (RESOLVED):** Update requires the existing `Customer.Write`
  permission.
- **OQ-005 (RESOLVED):** Update uses optimistic concurrency to prevent silent
  overwrites of concurrent changes.
- **OQ-006 (RESOLVED):** Each update supplies the client's previously obtained
  Customer version. A missing version returns HTTP 400 without updating; a
  stale version returns HTTP 409 without updating and identifies the stale
  Customer version as the cause. A successful update requires a matching
  version and changes it. The version's representation, transport,
  persistence, and validation mechanism are intentionally unspecified and are
  deferred to Plan.
