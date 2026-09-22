# Project Constitution

## Purpose
This project uses Spec-Driven Development. Requirements are specified before
technical design, tasks are created before coding, and completed work is audited.

## Engineering
- Follow the existing project architecture and conventions.
- Prefer clear separation of business, application, and infrastructure concerns.
- Prefer dependency injection and explicit dependencies.
- Reuse existing patterns before introducing abstractions.
- Do not introduce dependencies without justification.

## Security
- Never hard-code secrets.
- Validate external input.
- Enforce authorization where required.
- Never log credentials, tokens, or sensitive data.

## Testing
- Test business behavior.
- Use unit, integration, API/contract, or end-to-end tests when justified.
- Tests must validate requirements and acceptance criteria.

## Quality
- Keep changes focused.
- Avoid unrelated refactoring.
- Preserve backward compatibility unless explicitly changed.
- Build and test before declaring implementation complete.
