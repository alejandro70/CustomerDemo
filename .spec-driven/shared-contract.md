# Shared Spec-Driven Contract

## Variables
- FEATURE_ID: stable feature identifier, e.g. `001-customer-management`
- FEATURE_DIR: `specs/<FEATURE_ID>`
- SPEC_FILE: `<FEATURE_DIR>/spec.md`
- PLAN_FILE: `<FEATURE_DIR>/plan.md`
- TASKS_FILE: `<FEATURE_DIR>/tasks.md`

## Artifact ownership
- `constitution.md`: project owner; downstream agents read only.
- `spec.md`: Specify owns; downstream agents read and report conflicts.
- `plan.md`: Plan owns; downstream agents read and report conflicts.
- `tasks.md`: Tasks owns; Converge may append remediation tasks.
- source/tests: Implement owns.
- convergence report: Converge owns.

Downstream agents must not silently rewrite upstream decisions.

## IDs
FR-###, BR-###, NFR-###, AC-###, EC-###, PD-###, T###, RT###.

IDs are stable and must not be reused for a different meaning.

## Traceability
Requirement → Design → Task → Code → Test.

Every FR/BR/relevant NFR maps to tasks. Every AC has validation evidence.
Important PD decisions reference supported requirements.

## Status
Requirement: PASS, PARTIAL, FAIL, NOT VERIFIED.
Convergence: CONVERGED, NOT_CONVERGED.

## Handoffs
Specify → Plan: approved spec.md → plan.md
Plan → Tasks: spec.md + plan.md → dependency-ordered tasks.md
Tasks → Implement: spec.md + plan.md + tasks.md → code/tests
Implement → Converge: repository + artifacts → verification/remediation
Converge → Implement: RT### remediation tasks → fixes/tests

## Definition of Done
All requirements and acceptance criteria pass; relevant tests pass; meaningful
plan deviations are justified; no critical correctness/security gaps remain;
convergence is CONVERGED.
