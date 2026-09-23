# Project Instructions
This repository follows Spec-Driven Development: specify → clarify → plan → tasks → implement → converge.

Run Spec Kit Clarify after Specify and before Plan whenever a specification has
open questions, ambiguities, contradictions, or incomplete requirements.
Clarify records questions as `OQ-###`, obtains stakeholder decisions, and
updates the specification without inventing business decisions. Do not start
planning while a blocking question could change observable behavior, a business
rule, security policy, API contract, or required test coverage. Plan may begin
only after the specification is approved and clarification is resolved.

Authoritative artifacts:
1. `.spec-driven/constitution.md`
2. `spec.md`
3. `plan.md`
4. `tasks.md`

Never silently change upstream artifacts to make downstream work easier.

IDs:
- FR-### functional requirement
- BR-### business rule
- NFR-### non-functional requirement
- AC-### acceptance criterion
- EC-### edge case
- PD-### technical decision
- T### implementation task
- RT### convergence remediation task

Prefer existing patterns, focused changes, automated tests, secure defaults,
explicit error handling, and backward compatibility. Avoid speculative features
and unrelated refactoring.
