---
name: service-injection
description: Use when adding, reviewing, or refactoring dependency injection registrations, service abstractions, state contexts, injected collaborators, IServiceCollection extension methods, or service/processor/context interface naming in NTS and Not packages.
---

# Service Injection

Use constructor/property injection and DI registrations that express the consuming capability clearly.

## Defaults

- Register services by interface rather than implementation by default.
- Inject the interface in consumers instead of resolving services manually from `IServiceProvider`.
- Keep implementation-only registration only when there is a clear reason, such as framework constraints, direct concrete reuse, or a simple internal helper that is not a service boundary.

## Naming

- Name service interfaces as `I<Something>Service` when the dependency contains meaningful logic and exposes an ongoing capability.
- For narrow single-purpose abstractions, especially a single `Process`-style method, `I<Something>Processor` can be a better name.
- When the dependency is mostly a state holder with simple helper methods, name the interface `I<Something>Context`.
- Keep interface names aligned with the consumer capability, not the implementation mechanism.

## File Placement

- When there is one interface and one direct implementation, keep the interface in the same file below the implementation.
- When there are multiple interfaces, dependency inversion boundaries, or otherwise complex abstractions, prefer separate files for interfaces and implementations.
- Use separate namespaces too when that makes the dependency boundary clearer.

## Registration Shape

- Prefer central `IServiceCollection` extension methods for repeated or multi-service registrations.
- When an existing concrete service must remain resolvable, register the concrete once and map the interface to that same instance.

## Project Registration Extensions

- Each project that registers services should expose a public static `IServiceCollection` extension class named `<ProjectName>Services` in `<ProjectName>Services.cs`, using the repository's established short project name where one exists.
- The public registration method should be named `Add<ProjectName>`.
- The method should always take `this IServiceCollection services` and `IConfiguration configuration` as its first arguments. Add extra arguments only for required runtime context, such as a base URL or root assembly.
- The method should register all services needed by that project and chain-call dependent projects' service extension methods.
- Startup entry points such as `Program.cs` and `MauiProgram.cs` should call the top-level project registration method instead of manually calling each dependent project's registrations.
