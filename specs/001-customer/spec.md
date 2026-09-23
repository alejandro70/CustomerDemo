# Customer Feature Specification

## Status

- Specification status: APPROVED
- Clarification status: RESOLVED

## Problem

The CustomerApi needs a reliable way to create customer records and retrieve an
individual customer record. Today, consumers cannot manage this core customer
information through the API.

## Value

Consumers can register customers once and later retrieve their identifying and
contact information using the customer's identifier, while preventing duplicate
email records and rejecting incomplete or malformed submissions.

## Goals

- Create a customer with first name, last name, and email address.
- Retrieve an existing customer by identifier.
- Preserve the identifier and creation time assigned to each customer.
- Enforce required names, required valid email addresses, and email uniqueness.
- Provide automated verification of the feature's observable behavior.

## Non-Goals

- Updating or deleting customers.
- Listing or searching customers.
- Customer authentication, authorization, or account lifecycle management.
- Email ownership verification or notification delivery.

## Actors and Scenarios

### API Consumer

1. The consumer submits valid customer data to create a customer.
2. The API returns the created customer, including its assigned identifier and
   creation time.
3. The consumer uses the identifier to retrieve the customer later.
4. The API returns the matching customer.

### Invalid Submission

1. The consumer submits missing first name, last name, or email, or an email
   that does not have a valid email format.
2. The API rejects the submission without creating a customer.

### Duplicate Email

1. A customer already exists with an email address.
2. The consumer attempts to create another customer with that same email
   address.
3. The API rejects the duplicate and does not create another customer.

### Missing Customer

1. The consumer requests a customer identifier that does not identify an
   existing customer.
2. The API reports that the customer was not found.

### Unauthorized Consumer

1. A consumer attempts to create or retrieve a customer without authorization.
2. The API denies access and does not perform the requested operation.

## Functional Requirements

- **FR-001:** The API shall allow a consumer to create a customer by providing
  `FirstName`, `LastName`, and `Email`.
- **FR-002:** Upon successful creation, the API shall create exactly one
  customer with an `Id`, the submitted `FirstName`, `LastName`, and `Email`,
  and a `CreatedAt` timestamp.
- **FR-003:** The API shall return HTTP 201 when customer creation succeeds.
- **FR-004:** The API shall allow a consumer to retrieve a customer by `Id`.
- **FR-005:** When a requested customer exists, the API shall return the
  customer's `Id`, `FirstName`, `LastName`, `Email`, and `CreatedAt`, with HTTP
  200.
- **FR-006:** When no customer exists for a requested `Id`, the API shall
  return HTTP 404.
- **FR-007:** The API shall reject invalid customer creation data with HTTP 400.
- **FR-008:** The API shall permit customer creation and customer retrieval
  only to authorized users.
- **FR-009:** When customer creation is rejected because the normalized email
  already exists, the API shall return HTTP 409 with error code
  `CUSTOMER_EMAIL_EXISTS`.
- **FR-010:** When a requested customer `Id` has an invalid format, the API
  shall return HTTP 400.
- **FR-011:** The API shall authenticate requests using OAuth 2.0/OpenID
  Connect JWT Bearer access tokens issued by Microsoft Entra ID. ASP.NET Core
  shall validate the tokens, and policy-based authorization shall require
  `Customer.Write` to create a customer and `Customer.Read` to retrieve one.
- **FR-012:** An unauthenticated request or a request with an invalid access
  token shall return HTTP 401. An authenticated request that does not satisfy
  the required authorization policy shall return HTTP 403.

## Business Rules

- **BR-001:** `FirstName` is required to create a customer.
- **BR-002:** `LastName` is required to create a customer.
- **BR-003:** `Email` is required to create a customer.
- **BR-004:** After trimming surrounding whitespace and normalizing to
  lowercase, `Email` must satisfy the standard ASP.NET Core email-address
  validation policy to create a customer.
- **BR-005:** No more than one customer may have the same email address.
- **BR-006:** A rejected creation request, including a duplicate-email request,
  shall not create a customer record.
- **BR-007:** An unauthorized user shall not create or retrieve a customer.
- **BR-008:** Before validation, persistence, and uniqueness comparison, the
  API shall trim leading and trailing whitespace from an email address and
  normalize it to lowercase.
- **BR-009:** Email uniqueness shall be case-insensitive after normalization.
- **BR-010:** `FirstName` and `LastName` values containing only whitespace
  are invalid and shall be rejected as missing values.

## Non-Functional Requirements

- **NFR-001:** Automated tests shall verify all acceptance criteria, including
  successful creation and retrieval, invalid input rejection, duplicate-email
  rejection, retrieval of a nonexistent customer, and authorization
  enforcement.
- **NFR-002:** Validation failure responses shall not expose internal system
  details.
- **NFR-003:** Authorization tests shall cover JWT Bearer authentication with
  Microsoft Entra ID configuration and policy-based authorization behavior.

## Edge Cases

- **EC-001:** A create request missing `FirstName` is invalid.
- **EC-002:** A create request missing `LastName` is invalid.
- **EC-003:** A create request missing `Email` is invalid.
- **EC-004:** A create request whose normalized email fails the standard ASP.NET
  Core email-address validation policy is invalid.
- **EC-005:** A create request using an email already associated with a customer
  is rejected and leaves the existing customer unchanged.
- **EC-006:** A retrieval request for an identifier with no matching customer
  returns HTTP 404.
- **EC-007:** An unauthorized request to create or retrieve a customer is
  denied and does not disclose customer data.
- **EC-008:** A create request whose normalized email matches an existing
  customer's normalized email returns HTTP 409 with error code
  `CUSTOMER_EMAIL_EXISTS` and leaves the existing customer unchanged.
- **EC-009:** A create request with a whitespace-only `FirstName` or
  `LastName` returns HTTP 400 and does not create a customer.
- **EC-010:** A retrieval request with an invalidly formatted customer `Id`
  returns HTTP 400.
- **EC-011:** A create request with `Email` equal to
  `  John@Example.COM  ` creates a customer whose stored and returned email
  is `john@example.com`.
- **EC-012:** An unauthenticated or invalid-token request returns HTTP 401;
  an authenticated request lacking `Customer.Write` for creation or
  `Customer.Read` for retrieval returns HTTP 403.

## Acceptance Criteria

- **AC-001:** Given valid first name, last name, and email data, when a
  consumer creates a customer, then the response is HTTP 201 and represents a
  customer with a non-empty `Id`, the submitted values, and a `CreatedAt`
  timestamp.
- **AC-002:** Given a successfully created customer, when a consumer retrieves
  it by its `Id`, then the response is HTTP 200 and returns the same `Id`,
  `FirstName`, `LastName`, `Email`, and `CreatedAt` values.
- **AC-003:** Given no customer exists for an `Id`, when a consumer retrieves
  that `Id`, then the response is HTTP 404.
- **AC-004:** Given a create request with a missing first name, missing last
  name, missing email, or an email that fails the standard ASP.NET Core
  email-address validation policy after normalization, when the consumer
  submits it, then the response is HTTP 400 and no customer is created.
- **AC-005:** Given a customer exists with an email address, when a consumer
  creates another customer with that email address, then the request is
  rejected and no second customer is created.
- **AC-006:** Automated tests demonstrate AC-001 through AC-005 and AC-007.
- **AC-007:** Given a consumer is not authorized, when the consumer attempts
  to create or retrieve a customer, then the API denies access and does not
  create a customer or return customer data.
- **AC-008:** Given a customer exists with email `john@example.com`, when a
  consumer creates a customer using `  John@Example.COM  ` as the email, then
  the response is HTTP 409 with error code `CUSTOMER_EMAIL_EXISTS` and no
  second customer is created.
- **AC-009:** Given a consumer creates a customer with email
  `  John@Example.COM  `, when creation succeeds, then the returned customer
  email is `john@example.com`.
- **AC-010:** Given a create request with a whitespace-only first or last
  name, when the consumer submits it, then the response is HTTP 400 and no
  customer is created.
- **AC-011:** Given a request for a customer using an invalidly formatted
  identifier, when the consumer retrieves it, then the response is HTTP 400.
- **AC-012:** Given a request without a valid JWT Bearer access token, when
  the consumer creates or retrieves a customer, then the response is HTTP 401
  and no customer data is disclosed or created.
- **AC-013:** Given an authenticated consumer lacking `Customer.Write` or
  `Customer.Read` for the requested operation, when the consumer creates or
  retrieves a customer, then the response is HTTP 403 and no customer data is
  disclosed or created.

## Assumptions

- The API assigns the `Id` and `CreatedAt` values at successful creation; API
  consumers do not supply authoritative values for these fields.
- An `Id` is an API-supported customer identifier, regardless of its underlying
  representation.
- Email addresses are trimmed and normalized to lowercase before persistence
  and uniqueness comparison.

## Resolved Questions

- **OQ-001 (RESOLVED):** Duplicate normalized emails return HTTP 409 with
  error code `CUSTOMER_EMAIL_EXISTS`. Traced to FR-009, BR-006, EC-008, and
  AC-008.
- **OQ-002 (RESOLVED):** Email uniqueness is case-insensitive. Traced to
  BR-009, EC-008, and AC-008.
- **OQ-003 (RESOLVED):** After normalization, email syntax is validated using
  the standard ASP.NET Core email-address validation policy. Traced to BR-004,
  EC-004, and AC-004.
- **OQ-004 (RESOLVED):** Whitespace-only names are missing values and return
  HTTP 400. Traced to BR-010, EC-009, and AC-010.
- **OQ-005 (RESOLVED):** Invalidly formatted customer identifiers return HTTP
  400. Traced to FR-010, EC-010, and AC-011.
- **OQ-006 (RESOLVED):** The API uses Microsoft Entra ID OAuth 2.0/OpenID
  Connect JWT Bearer authentication with ASP.NET Core token validation and
  policy-based authorization. Creation requires `Customer.Write`, retrieval
  requires `Customer.Read`, unauthenticated or invalid-token requests return
  HTTP 401, and authenticated requests lacking the required policy return HTTP
  403. Traced to FR-011, FR-012, EC-012, AC-012, and AC-013.
