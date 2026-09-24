# Spec-Driven VS Code / GitHub Copilot Agents

Copy `.github/` and `.spec-driven/` into the root of a repository.

Reload VS Code, open Copilot Chat, and select:
- Spec Kit Specify
- Spec Kit Clarify
- Spec Kit Plan
- Spec Kit Tasks
- Spec Kit Implement
- Spec Kit Converge

Workflow:
Specify → Clarify → Plan → Tasks → Implement → Converge

Use Spec Kit Clarify before planning when `spec.md` contains open questions,
ambiguities, contradictions, or incomplete requirements. Clarify assigns
`OQ-###` identifiers, presents blocking decisions for stakeholder input, and
updates the specification after decisions are provided. It does not invent
business decisions or create a plan while blocking questions remain.

If Converge finds gaps, it appends RT### remediation tasks to tasks.md.
Run Implement again, then Converge again.

Recommended feature layout:
`specs/001-feature-name/{spec.md,plan.md,tasks.md}`

## Customer API Operations

The service applies the customer EF Core migration during startup before it
accepts traffic. Readiness is exposed at `/health/ready` and includes the
database dependency check.

Configure these values through deployment configuration or secret management:

- `ConnectionStrings__CustomerDatabase`: relational database connection string.
- `Entra__Authority`: Microsoft Entra OpenID Connect authority.
- `Entra__Audience`: API application identifier URI or client ID expected by
	access tokens.

Do not commit production connection strings or credentials. The API requires a
valid JWT Bearer token; `Customer.Write` is required for `POST /customers` and
`Customer.Read` is required for `GET /customers/{id}`. Permissions may be
present in delegated `scp` or application `roles` claims. Routine request
telemetry records only status, endpoint, and trace identifier; it excludes
emails, authorization headers, and tokens.
