# Spec-Driven VS Code / GitHub Copilot Agents

Copy `.github/` and `.spec-driven/` into the root of a repository.

Reload VS Code, open Copilot Chat, and select:
- Spec Kit Specify
- Spec Kit Plan
- Spec Kit Tasks
- Spec Kit Implement
- Spec Kit Converge

Workflow:
Specify → Plan → Tasks → Implement → Converge

If Converge finds gaps, it appends RT### remediation tasks to tasks.md.
Run Implement again, then Converge again.

Recommended feature layout:
`specs/001-feature-name/{spec.md,plan.md,tasks.md}`
