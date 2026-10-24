---
name: solid
description: "Use when adding, reviewing, or refactoring object-oriented or service-oriented code where SOLID design, dependency boundaries, class/interface responsibilities, polymorphism, abstraction, or maintainability tradeoffs matter. Apply the SOLID principles through a practical lens: improve cohesion, substitutability, and dependency direction without adding abstraction ceremony that the current code does not need."
---

# SOLID Design

## Overview

Use SOLID as design pressure, not design law. Let the principles expose coupling, unclear responsibilities, brittle inheritance, and unnecessary dependencies; then choose the smallest change that makes the code easier to understand, test, and evolve.

Always view SOLID through a practical rather than dogmatic lens. Do not create interfaces, inheritance hierarchies, factories, or abstractions solely to satisfy a principle in the abstract. Prefer simple, cohesive concrete code until real variation, testing pressure, dependency direction, or reuse makes abstraction earn its keep.

## Review Workflow

When applying SOLID to a change:

1. Identify the behavior being changed, the clients that call it, and the likely future variation points.
2. Check responsibility first: determine whether each type has one clear reason to change.
3. Check dependency direction: keep policy and domain decisions independent from framework, storage, transport, and UI details.
4. Check contracts: verify implementations can be substituted without surprising callers.
5. Check client shape: keep interfaces and collaborator APIs focused on what their callers actually use.
6. Choose the least ceremonial fix that improves the code today and leaves a clearer path for tomorrow.
7. Add or adjust tests at the level that owns the behavior.

## Single Responsibility

Give a class, component, or service one coherent reason to change.

- Split when unrelated reasons to change are mixed, such as business rules plus persistence details, formatting plus workflow orchestration, or UI state plus domain decisions.
- Keep together code that changes for the same feature reason, even when it contains several small helper methods.
- Prefer intention-revealing collaborators over generic `Manager`, `Helper`, or `Processor` types when the responsibility has a specific domain meaning.
- Treat size as a clue, not proof. A small type can have mixed responsibilities; a larger type can still be cohesive.

## Open/Closed

Make code open for realistic extension and closed against unnecessary churn.

- Use polymorphism, strategies, options, or data-driven rules when there are multiple known variants or a variant is likely soon.
- Keep simple conditionals when the variation is small, stable, and clearer inline.
- Avoid speculative extension points that make the first implementation harder to read.
- Localize change-prone decisions so adding a variant affects one obvious place rather than scattered callers.

## Liskov Substitution

Ensure implementations honor the promises of their base type or interface.

- Do not strengthen preconditions, weaken postconditions, throw surprising exceptions, or silently ignore required behavior.
- Keep interface and base-class contracts explicit when callers depend on subtle behavior.
- Prefer composition over inheritance when a subtype would need to disable, fake, or reinterpret inherited behavior.
- Test through the abstraction when multiple implementations must behave consistently.

## Interface Segregation

Shape interfaces around client needs.

- Split interfaces when consumers are forced to depend on members they do not use.
- Keep interfaces together when the members form one capability that clients naturally consume as a unit.
- Avoid "one method per interface" fragmentation unless the call sites and dependency graph actually become clearer.
- Let command/query boundaries, read/write separation, or role-specific clients guide splits when those distinctions exist in the code.

## Dependency Inversion

Make high-level policy depend on stable abstractions, not volatile details.

- Put abstractions near the layer that owns the policy or use case, not automatically beside the implementation.
- Depend on interfaces or ports for storage, HTTP, clocks, files, message buses, UI frameworks, and other external boundaries.
- Do not introduce an interface for every concrete class by default. A concrete dependency is fine when it is local, stable, side-effect-light, and not a testing or boundary problem.
- Keep dependency injection composition separate from business decisions.

## Practical Tradeoffs

Prefer the design that reduces total cost for this codebase.

- Choose clarity over pattern completeness.
- Introduce abstraction when it removes duplication, isolates volatility, improves tests, or protects a boundary.
- Remove abstraction when it hides simple behavior, has only one forced implementation, or makes navigation harder without buying flexibility.
- Follow nearby repo conventions before introducing a new design shape.
- When SOLID principles point in different directions, name the tradeoff and optimize for the change the code is most likely to face.

## Related NTS Skills

Use `$logic-separation` when SOLID questions involve Domain, Application, Infrastructure, or Adapter placement.

Use `$service-injection` when dependency inversion affects DI registration, service lifetimes, or injected collaborator conventions.

Use `$service-helper-collaborator-definitions` when deciding whether a responsibility should be a Service, Helper, Processor, or more specific collaborator.
