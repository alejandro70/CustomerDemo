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
