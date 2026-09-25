# End-to-End Validation of a Spec-Driven .NET 8 Project

**Customer Feature — Specify → Clarify → Plan → Tasks → Implement → Converge**

## Purpose

This document describes the practical procedure for validating the custom Spec-Driven Development workflow against a real .NET 8 project using a simple **Customer** feature.

The objective is to verify:

- End-to-end agent handoff.
- Requirement traceability.
- Explicit resolution of open questions.
- Technical planning.
- Dependency-ordered implementation tasks.
- Code and automated tests.
- Independent convergence verification.
- Remediation and re-convergence.

---

# 1. Create the .NET 8 solution

Create a minimal ASP.NET Core Web API and an xUnit test project.

```powershell
mkdir CustomerDemo
cd CustomerDemo

dotnet new sln -n CustomerDemo

mkdir src
mkdir tests

dotnet new webapi -n CustomerApi -o src/CustomerApi --framework net8.0

dotnet new xunit -n CustomerApi.Tests -o tests/CustomerApi.Tests --framework net8.0

dotnet sln add src/CustomerApi/CustomerApi.csproj

dotnet sln add tests/CustomerApi.Tests/CustomerApi.Tests.csproj

dotnet add tests/CustomerApi.Tests/CustomerApi.Tests.csproj reference src/CustomerApi/CustomerApi.csproj
```

## 1.1 Verify the baseline

Before introducing the Customer feature, execute:

```powershell
dotnet restore
dotnet build
dotnet test
```

The project should be **green before the agents modify it**.

This provides a clean baseline for identifying regressions introduced during implementation.

---

# 2. Install the Spec-Driven package

Copy the following directories from the generated `spec-driven-vscode-copilot-agents` package into the root of `CustomerDemo`:

```text
CustomerDemo/
├── .github/
│   ├── copilot-instructions.md
│   └── agents/
│       ├── speckit-specify.agent.md
│       ├── speckit-plan.agent.md
│       ├── speckit-tasks.agent.md
│       ├── speckit-implement.agent.md
│       └── speckit-converge.agent.md
│
├── .spec-driven/
│   ├── constitution.md
│   └── shared-contract.md
│
├── src/
├── tests/
└── CustomerDemo.sln
```

Open the project in Visual Studio Code:

```powershell
code .
```

Reload VS Code if necessary so that the custom agents become available.

---

# 3. Specify the Customer feature

Open GitHub Copilot Chat and select:

**Spec Kit Specify**

Provide the business requirement without prescribing the implementation.

Example:

> Create a Customer feature for the .NET 8 CustomerApi.
>
> A customer has an Id, FirstName, LastName, Email and CreatedAt.
>
> The API must allow creating a customer and retrieving a customer by Id.
>
> Email is required and must have a valid email format.
>
> FirstName and LastName are required.
>
> A customer cannot be created with a duplicate email.
>
> Creating a customer returns HTTP 201.
>
> Retrieving an existing customer returns HTTP 200.
>
> Retrieving a nonexistent customer returns HTTP 404.
>
> Invalid customer data returns HTTP 400.
>
> The feature must be covered by automated tests.

## Expected artifact

```text
specs/001-customer/spec.md
```

The specification should contain, as applicable:

- Problem statement.
- Business value.
- Goals.
- Non-goals.
- Actors.
- Scenarios.
- Functional requirements.
- Business rules.
- Non-functional requirements.
- Edge cases.
- Acceptance criteria.
- Assumptions.
- Open Questions.

Use stable IDs:

```text
FR-###   Functional Requirement
BR-###   Business Rule
NFR-###  Non-Functional Requirement
AC-###   Acceptance Criterion
EC-###   Edge Case
```

At this point the specification describes **WHAT** and **WHY**, not HOW.

---

# 4. Address Open Questions with Clarify

Open Questions represent unresolved decisions that may affect:

- Business behavior.
- API contracts.
- Security.
- Testing.
- Architecture.
- Data persistence.
- Error handling.

The agent should **not invent answers** to these questions.

The recommended workflow is:

```text
Specify
   ↓
Clarify
   ↓
Plan
```

The Clarify stage should:

1. Identify unresolved or ambiguous decisions.
2. Classify their impact.
3. Present the question to the appropriate stakeholder.
4. Record the decision.
5. Update the specification.
6. Remove or mark the question as resolved.
7. Block Plan when a decision is required before technical design can proceed.

---

## 4.1 Customer Open Questions

The initial Customer specification may contain questions such as:

### OQ-001 — Duplicate email

What HTTP status and response contract should be used when creation is rejected because the email already exists?

### OQ-002 — Email case sensitivity

Is email uniqueness case-sensitive or case-insensitive?

For example:

```text
john@example.com
John@example.com
JOHN@EXAMPLE.COM
```

Should these represent the same email?

### OQ-003 — Email validation and normalization

What exact email-validation standard and normalization behavior should apply?

Consider:

- Leading/trailing whitespace.
- Case normalization.
- Storage format.
- Comparison format.
- Accepted characters.
- Maximum length.

### OQ-004 — Whitespace-only names

Should this be considered missing?

```json
{
  "firstName": "   ",
  "lastName": "Smith"
}
```

### OQ-005 — Malformed customer identifiers

Should an invalid customer identifier return:

```text
400 Bad Request
```

or:

```text
404 Not Found
```

A useful distinction is:

```text
Malformed ID
    ↓
400 Bad Request

Valid ID but customer does not exist
    ↓
404 Not Found
```

### OQ-006 — Authentication and authorization

What authentication mechanism and authorization policy should protect Customer endpoints?

---

# 4.2 Recommended Authentication Mechanism

For the Customer API reference project, use:

> **OAuth 2.0 / OpenID Connect with JWT Bearer access tokens**

Conceptually:

```text
Client
   │
   │ Authenticate
   ▼
Identity Provider
   │
   │ JWT Access Token
   ▼
Client
   │
   │ Authorization: Bearer <token>
   ▼
Customer API
   │
   ├── Validate signature
   ├── Validate issuer
   ├── Validate audience
   ├── Validate expiration
   │
   ▼
Authorization
```

The identity provider should remain configurable.

For an enterprise Microsoft environment, **Microsoft Entra ID** is an appropriate example.

The business/application logic should not be tightly coupled to a specific identity provider.

---

# 4.3 Recommended Authorization Policy

Use:

> **Policy-based authorization**

Define permissions/capabilities rather than hard-coding application roles.

Recommended permissions:

```text
Customer.Read
Customer.Write
Customer.Admin
```

Example authorization matrix:

| Operation | Required permission |
|---|---|
| `GET /customers/{id}` | `Customer.Read` |
| `POST /customers` | `Customer.Write` |
| `PUT /customers/{id}` | `Customer.Write` |
| `DELETE /customers/{id}` | `Customer.Admin` |

This allows the identity provider to map its own roles/groups to application capabilities.

For example:

```text
Identity Provider
       │
       ├── Customer Service Representative
       │        └── Customer.Read
       │
       └── Customer Manager
                ├── Customer.Read
                └── Customer.Write
```

The API therefore depends on:

```text
Customer.Read
Customer.Write
Customer.Admin
```

rather than on provider-specific role names.

---

# 4.4 Authentication and Authorization HTTP Contracts

These behaviors should be explicit in the specification.

## Authentication failure

If the request has no valid authentication:

```http
401 Unauthorized
```

Examples:

- Missing access token.
- Invalid token.
- Expired token.
- Invalid issuer.
- Invalid audience.

## Authorization failure

If the user is authenticated but lacks the required permission:

```http
403 Forbidden
```

The distinction is:

```text
No valid identity
       ↓
401 Unauthorized

Valid identity
       +
Insufficient permission
       ↓
403 Forbidden
```

---

# 4.5 Convert Decisions into Requirements

Open Questions should eventually become explicit specification content.

For example, if the project decides that duplicate emails return `409 Conflict`:

```markdown
### BR-001 — Customer email uniqueness

Customer email addresses must be unique.

If a customer is created using an email that already exists,
the API shall return HTTP 409 Conflict.

The response shall contain the error code:

CUSTOMER_EMAIL_EXISTS
```

Then create an acceptance criterion:

```markdown
### AC-005 — Duplicate email

Given that a customer already exists with email
`john@example.com`,

when another customer is created using
`john@example.com`,

then the API shall return HTTP 409 Conflict,

and the response code shall be:

CUSTOMER_EMAIL_EXISTS
```

The traceability chain becomes:

```text
Open Question
     ↓
Decision
     ↓
Business Rule
     ↓
Acceptance Criterion
     ↓
Implementation Task
     ↓
Test
```

---

# 4.6 Blocking Questions

Not every Open Question necessarily blocks the technical plan.

Recommended structure:

```markdown
## Open Questions

| ID | Question | Impact | Blocking |
|---|---|---|---|
| OQ-001 | Duplicate email status | API contract | Yes |
| OQ-002 | Email case sensitivity | Business rule | Yes |
| OQ-003 | Email normalization | Business/API | Yes |
| OQ-004 | Whitespace names | Validation | Yes |
| OQ-005 | Invalid ID response | API contract | Yes |
| OQ-006 | Authorization | Security | Yes |
```

The specification should be considered ready for Plan only when blocking questions are resolved.

A useful conceptual state model is:

```text
DRAFT
  ↓
CLARIFICATION_REQUIRED
  ↓
CLARIFIED
  ↓
APPROVED
  ↓
READY_FOR_PLAN
```

---

# 5. Create the Technical Plan

After the specification has been clarified and approved, select:

**Spec Kit Plan**

Suggested prompt:

> Create the technical implementation plan for the approved Customer specification. Inspect the existing .NET 8 repository and follow its architecture and conventions.

## Expected artifact

```text
specs/001-customer/plan.md
```

The plan defines **HOW**.

It should cover, as applicable:

- Architecture.
- Components.
- Domain/application responsibilities.
- Persistence.
- APIs.
- Security.
- Authorization.
- Error handling.
- Reliability.
- Observability.
- Testing.
- Configuration.
- Migrations.
- Deployment.
- Risks.
- Trade-offs.
- Technical decisions.

Technical decisions use:

```text
PD-###
```

Example:

```text
PD-001 — Use policy-based authorization.

Rationale:
The API should depend on business capabilities rather than
provider-specific role names.
```

---

# 6. Generate Implementation Tasks

Select:

**Spec Kit Tasks**

Suggested prompt:

> Generate dependency-ordered implementation tasks for the Customer feature using the approved specification and technical plan. Ensure every requirement and acceptance criterion has traceable implementation and validation tasks.

## Expected artifact

```text
specs/001-customer/tasks.md
```

Tasks use:

```text
T###
```

Each task should contain, as applicable:

- Task description.
- Requirement references.
- Acceptance criterion references.
- Technical decision references.
- Target component/file.
- Dependencies.
- Expected result.
- Validation criteria.

Example:

```markdown
## T003 — Implement Customer creation endpoint

Requirements:
- FR-001
- FR-004
- BR-001

Acceptance Criteria:
- AC-001
- AC-005

Depends on:
- T001
- T002

Validation:
- Successful creation returns 201.
- Duplicate email returns 409.
```

The task graph should respect implementation dependencies.

---

# 7. Implement the Feature

Select:

**Spec Kit Implement**

Suggested prompt:

> Implement the incomplete Customer feature tasks in dependency order. Follow the specification, technical plan, tasks, constitution and shared contract. Build and test the solution as you work.

The agent should:

1. Read the specification.
2. Read the technical plan.
3. Read the task list.
4. Inspect the repository.
5. Implement tasks in dependency order.
6. Add or update tests.
7. Build the solution.
8. Execute tests.
9. Fix implementation issues.
10. Mark completed tasks.

The agent should avoid:

- Unrelated refactoring.
- Speculative functionality.
- Unnecessary dependencies.
- Changing requirements silently.
- Weakening tests to make them pass.
- Introducing architecture not justified by the plan.

---

# 8. Validate the Implementation

Run:

```powershell
dotnet build
dotnet test
```

Then validate the API behavior.

## Customer creation

```http
POST /customers
```

Valid request:

```json
{
  "firstName": "John",
  "lastName": "Smith",
  "email": "john.smith@example.com"
}
```

Expected:

```http
201 Created
```

## Customer retrieval

```http
GET /customers/{id}
```

Expected for an existing customer:

```http
200 OK
```

## Invalid customer data

Expected:

```http
400 Bad Request
```

## Duplicate email

Expected according to the approved specification, for example:

```http
409 Conflict
```

## Valid but nonexistent customer

Expected:

```http
404 Not Found
```

## Missing/invalid authentication

Expected:

```http
401 Unauthorized
```

## Insufficient authorization

Expected:

```http
403 Forbidden
```

---

# 9. Run Converge

Select:

**Spec Kit Converge**

Suggested prompt:

> Audit the Customer feature against the constitution, specification, technical plan, implementation tasks, source code, configuration and automated tests. Verify every requirement and acceptance criterion independently. Do not implement fixes.

Converge acts as an **auditor**, not as an implementation agent.

For every requirement it should report one of:

```text
PASS
PARTIAL
FAIL
NOT VERIFIED
```

For example:

```text
FR-001  PASS
FR-002  PASS
FR-003  PASS
FR-004  PASS
FR-005  PASS

AC-001  PASS
AC-002  PASS
AC-003  PASS
AC-004  PASS
AC-005  PASS

Build: PASS
Tests: PASS

Traceability: COMPLETE

Status: CONVERGED
```

---

# 10. Deliberately Introduce a Failure

This is an important validation of the agent workflow.

Intentionally change the nonexistent-customer behavior.

For example, change:

```csharp
return Results.NotFound();
```

to:

```csharp
return Results.Ok();
```

Run:

```powershell
dotnet test
```

Then run **Spec Kit Converge** again.

Expected result:

```text
AC-004  FAIL

Expected:
HTTP 404 Not Found

Actual:
HTTP 200 OK

Evidence:
Customer endpoint returns Results.Ok()
when the customer does not exist.

Status:
NOT_CONVERGED
```

This verifies that Converge is actually comparing implementation against the specification rather than simply trusting the task status.

---

# 11. Create a Remediation Task

When Converge identifies a genuine gap, it should add a remediation task to `tasks.md`.

Use:

```text
RT###
```

Example:

```markdown
## RT001 — Fix customer not-found response

Type:
Remediation

Requirement:
FR-006

Acceptance Criterion:
AC-004

Problem:
The API returns HTTP 200 when the customer does not exist.

Expected:
HTTP 404 Not Found.

Validation:
Add or update an integration test covering a valid
but nonexistent customer identifier.
```

The remediation task must preserve traceability to the original requirement and acceptance criterion.

---

# 12. Implement Remediation and Reconverge

Run **Spec Kit Implement** again.

Suggested prompt:

> Implement the outstanding RT remediation tasks identified by Converge. Do not modify the specification or architecture unless a contradiction is discovered.

After the fix:

```powershell
dotnet build
dotnet test
```

Then run:

**Spec Kit Converge**

Expected result:

```text
RT001  RESOLVED

AC-004  PASS

Build  PASS
Tests  PASS

Status: CONVERGED
```

---

# 13. End-to-End Traceability

The central validation of the workflow is the following chain:

```text
Requirement
     ↓
Technical Decision
     ↓
Implementation Task
     ↓
Source Code
     ↓
Automated Test
     ↓
Convergence Evidence
```

For example:

```text
FR-001
  ↓
PD-001
  ↓
T003
  ↓
CustomerEndpoints.cs
  ↓
CustomerApiTests.cs
  ↓
AC-001
  ↓
Converge: PASS
```

The goal is that a reviewer can start from a business requirement and determine exactly:

- Why the behavior exists.
- How it was designed.
- Which task implemented it.
- Where it was implemented.
- Which test verifies it.
- What evidence proves convergence.

---

# 14. Recommended Agent Workflow

The complete workflow is:

```text
                 ┌──────────────┐
                 │   Specify    │
                 │ WHAT / WHY   │
                 └──────┬───────┘
                        │
                        ▼
                 ┌──────────────┐
                 │   Clarify    │
                 │ Decisions    │
                 └──────┬───────┘
                        │
                        ▼
                 ┌──────────────┐
                 │     Plan     │
                 │    HOW       │
                 └──────┬───────┘
                        │
                        ▼
                 ┌──────────────┐
                 │    Tasks     │
                 │   T###       │
                 └──────┬───────┘
                        │
                        ▼
                 ┌──────────────┐
                 │  Implement   │
                 │ Code + Tests │
                 └──────┬───────┘
                        │
                        ▼
                 ┌──────────────┐
                 │   Converge   │
                 │   Audit      │
                 └──────┬───────┘
                        │
                 ┌──────┴──────┐
                 │             │
              PASS           FAIL
                 │             │
                 ▼             ▼
             CONVERGED      RT-###
                               │
                               ▼
                          IMPLEMENT
                               │
                               ▼
                           CONVERGE
```

---

# 15. Definition of Done

The Customer feature is complete when:

- [ ] Specification is approved.
- [ ] No blocking Open Questions remain.
- [ ] Every functional requirement has traceable implementation work.
- [ ] Every acceptance criterion has validation.
- [ ] Technical decisions are documented where necessary.
- [ ] `dotnet build` succeeds.
- [ ] `dotnet test` succeeds.
- [ ] Security behavior is verified where applicable.
- [ ] Authentication behavior is verified.
- [ ] Authorization behavior is verified.
- [ ] No critical convergence gaps remain.
- [ ] Deviations from the plan are documented and justified.
- [ ] Converge reports `CONVERGED`.

---

# 16. Final Project Structure

After the workflow is completed:

```text
CustomerDemo/
├── .github/
│   ├── copilot-instructions.md
│   └── agents/
│       ├── speckit-specify.agent.md
│       ├── speckit-plan.agent.md
│       ├── speckit-tasks.agent.md
│       ├── speckit-implement.agent.md
│       └── speckit-converge.agent.md
│
├── .spec-driven/
│   ├── constitution.md
│   └── shared-contract.md
│
├── specs/
│   └── 001-customer/
│       ├── spec.md
│       ├── plan.md
│       └── tasks.md
│
├── src/
│   └── CustomerApi/
│
├── tests/
│   └── CustomerApi.Tests/
│
└── CustomerDemo.sln
```

---

# 17. Final Objective of the Experiment

The objective is not merely to verify that Copilot can generate a Customer API.

The real objective is to validate a **controlled software-engineering workflow**:

```text
Business requirement
        ↓
Explicit specification
        ↓
Ambiguity resolution
        ↓
Technical design
        ↓
Traceable implementation tasks
        ↓
Implementation
        ↓
Automated validation
        ↓
Independent convergence
        ↓
Remediation when necessary
        ↓
Final convergence
```

A successful experiment demonstrates that the five original agents, plus the proposed Clarify stage, can maintain a consistent chain from **business intent to verified implementation**.
