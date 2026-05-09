---
name: logic-separation
description: "Use when adding, reviewing, or refactoring NTS code that involves DDD business logic placement. Apply NTS conventions for separating Domain, Application, Infrastructure, and Adapter responsibilities: keep invariants and state transitions in domain objects with DomainException validation, keep domain projects free of direct infrastructure dependencies, and move orchestration, domain events, repositories, HTTP/storage/message interactions, and framework adapter logic into DI-injected Application services or infrastructure implementations."
---

# NTS DDD Business Logic Separation

Separate behavior by what it protects or coordinates. Domain objects protect model validity. Application services coordinate use cases and boundaries. Infrastructure implements ports. Adapters translate frameworks into application calls.

## Required Workflow

When changing business behavior:

1. Identify the use case, affected aggregate/entity/value object, state transition, and external dependencies.
2. Put rules that decide whether a model state or transition is valid inside the domain object that owns that state.
3. Put rules that coordinate repositories, external systems, domain boundaries, domain events, transactions, or multiple domain objects into an Application service.
4. Put storage, HTTP, message bus, Azure Functions, SignalR, framework, serialization, and SDK details behind abstractions implemented outside the domain.
5. Keep adapters limited to binding framework inputs, invoking Application services, and shaping framework results.
6. Add focused tests at the layer that owns the behavior.

## Domain Layer

Use domain objects for hard domain logic: invariants, valid states, state transitions, and facts that must always be true for the model.

Domain objects must:

- Prevent invalid state instead of allowing invalid state and repairing it later.
- Validate before mutating state.
- Throw `DomainException` for business-rule rejection, invalid transitions, missing required domain facts, or combinations that violate an invariant.
- Prefer intention-revealing methods such as `Start`, `Deactivate`, `Register`, `Eliminate`, `Reschedule`, or `ApplyResult` over external property mutation.
- Keep constructors/factories and mutation methods responsible for their own invariants.
- Leave persistence, transport, notification, clock, storage, and framework behavior outside unless accessed through a pure abstraction.

Domain projects must not reference direct infrastructure dependencies such as storage repositories, Mongo/EF clients, HTTP clients, Azure Functions, SignalR, message bus SDKs, app projects, or UI projects.

When a domain rule needs external knowledge, prefer this order:

1. Have an Application service fetch the needed facts and pass values or decisions into the domain method.
2. Use a domain service/port interface whose contract uses domain language and domain/application types only.
3. Implement that interface in Application or Infrastructure and inject it from outside the domain.

## Application Layer

Use DI-injected Application services for use cases and cross-boundary work.

Application services may:

- Load and save aggregates through repository abstractions.
- Coordinate multiple aggregates, repositories, bounded contexts, app services, or external ports.
- Call HTTP/storage/message abstractions.
- Emit, collect, persist, or publish domain events after domain objects record or produce them.
- Manage transaction boundaries, idempotency, retries, and integration sequencing.
- Map contracts/models to domain calls and map domain results back to contracts/models.
- Decide which domain operation to invoke for a request.

Application services must not:

- Bypass domain methods by setting fields/properties to force state.
- Recreate invariants that belong inside the aggregate/entity/value object.
- Let infrastructure types leak into domain method signatures.
- Hide domain invalidity as nulls, booleans, or silent no-ops when the domain should reject with `DomainException`.

## Infrastructure And Adapters

Infrastructure implements ports and persistence/transport details. Adapters are Azure Functions, controllers, minimal API endpoints, SignalR hubs, Blazor components, component-behind classes, jobs, and other framework entrypoints.

Adapters may bind inputs, read route/body/form values, perform framework logging/telemetry, manage UI-only state, invoke Application services, and shape framework-specific responses. They must not contain business rules, multi-step workflows, domain state transitions, repository orchestration, or domain event publishing logic.

Simple CRUD pass-through can remain thin only when there is no business meaning beyond persistence. Once there is domain meaning, state transition, eligibility, validity, visibility, active/past filtering, or cross-boundary coordination, move it to Domain or Application.

## Placement Smells

Move logic into Domain when you see:

- A service, adapter, or repository setting several fields to represent one domain transition.
- Repeated validation of the same aggregate/entity/value object state outside the object.
- Conditionals that decide whether a transition such as start, reset, deactivate, eliminate, or register is legal.
- Invalid combinations that can be persisted and detected only later.
- Exceptions for domain invalidity thrown outside the object that owns the invalid state.

Move logic into Application when you see:

- More than one repository or external port call for a single use case.
- Coordination across domain boundaries, apps, integrations, messages, or storage.
- Domain events being published from adapters or repositories.
- HTTP, storage, message, retry, transaction, authorization, or integration concerns mixed into domain objects.
- Logic likely to be reused by another endpoint, hub, UI component, job, test harness, or app.

Keep logic in adapters only when it is framework or presentation plumbing.

## NTS Placement

Use these default homes:

- Domain model and invariant behavior: `src/Domain/NTS.Domain*`.
- Shared application use cases, ports, factories, and orchestration: `src/NTS.Application*`.
- Storage and direct infrastructure implementations: `src/NTS.Storage` or app infrastructure projects.
- Azure Functions, Blazor components, hubs, controllers, and app entrypoints: `src/Apps/*`.

Prefer `src/NTS.Application` for reusable Application services. App-specific services may live near the app when the behavior is genuinely app-local, but still treat them as Application services: inject them, keep adapters thin, keep domain state changes inside domain objects, and keep infrastructure behind ports.

Use interfaces for injected collaborators, implement services with the repo's injection marker such as `ITransient` when appropriate, and see `$service-helper-collaborator-definitions` when deciding between a Service, Helper, Processor, or more specific collaborator.

## Testing Expectations

Test the owning layer:

- Domain tests instantiate domain objects directly, exercise valid transitions, assert invalid transitions throw `DomainException`, and verify failed transitions leave state unchanged.
- Application service tests mock repositories/ports, verify orchestration, saved state, domain event publication, and infrastructure boundary calls.
- Adapter tests verify binding, authorization/response behavior, and delegation to Application services without duplicating domain tests.
