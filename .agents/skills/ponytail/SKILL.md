---
name: ponytail
description: >-
  Enforces the official Ponytail minimalism methodology (The Seven-Rung Ladder).
  Use whenever planning, designing, refactoring, or writing code to ensure radical
  simplicity, eliminate overengineering, and produce the smallest correct implementation.
---

# Ponytail Skill: Radical Minimalism for Engineering

The Ponytail skill directs the agent to act like a pragmatic senior developer who solves problems with the smallest possible change.

## The Seven-Rung Decision Ladder

When responding to any engineering request, step down The Ladder sequentially:

1. **YAGNI**: Does this feature or abstraction genuinely need to exist right now? If not, do not write it.
2. **Codebase Reuse**: Can an existing service, component, or utility in Finora satisfy this?
3. **Standard Library**: Does the .NET BCL or TypeScript/Web standard provide this out of the box?
4. **Native Platform**: Can native browser, CSS, or PostgreSQL SQL capabilities solve this directly?
5. **Installed Dependency**: Does NG-ZORRO, Tailwind CSS 4, or EF Core already provide this?
6. **One-Liner**: Can this logic be cleanly and transparently expressed in one line?
7. **Minimum Viable Code**: What is the smallest correct implementation that preserves validation, security, and tests?

## Operational Rules

- **Examine Context First**: Read all touched files, schemas, and API contracts before proposing code changes.
- **Honor Safety Boundaries**: Never compromise financial math, authentication, household tenancy isolation, input validation, or test execution.
- **Document Shortcuts**: Mark deliberate short-term compromises with:
  `// ponytail: <limitation>, upgrade path: <planned upgrade>`
- **Report Minimal Diffs**: Deliver clear, focused diffs without unnecessary reformats or collateral edits.

