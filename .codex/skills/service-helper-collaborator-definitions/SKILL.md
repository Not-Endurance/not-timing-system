---
name: service-helper-processor-definitions
description: Use when deciding whether new NTS logic belongs in a Service, Helpe or another purpose-specific collaborator, especially when refactoring business logic out of Functions or shared UI/application code.
---

# Service, Helper, Collaborator  Definitions

Use the smallest collaborator name that honestly describes the responsibility. The boundary is decided by what the code means, not by file size.

## Service

Use a Service for application or business behavior:

- Implements a use case or feature workflow.
- Applies business rules or policy.
- Coordinates repositories, sockets, other services, or domain factories.
- Performs state transitions such as start, deactivate, reset, connect, publish, or reconcile.
- Usually DI-served because it has dependencies or is part of app composition.

## Helper

Use a Helper for general-purpose utility code:

- No business meaning.
- Deterministic, reusable, and small.
- Does not own a workflow.
- Can be static when it has no DI dependencies.
- Examples: date normalization, formatting, null-safe collection helpers, simple mapping conveniences.

## Specific collaborator

Use a specific collaborator bounded procedural flow that is not itself a business service:

- Transforms or prepares data through multiple steps.
- May be DI-served if it needs dependencies.
- Has a clear input/output and limited side effects.
- Examples: initializer, import preparation, batch document normalization


## Decision Rule

If the code answers "what should the system do for this user/domain state?", it is Service code. If it answers "how do I transform, parse, format, or record this data?", choose Helper, Processor, Parser, Logger, or another specific noun.
