---
name: mongodb
description: "Use when adding, reviewing, or refactoring NTS MongoDB code, including repositories, filters, update definitions, indexes, and MongoDB.Driver usage. Apply NTS MongoDB conventions: keep update definition builders simple and side-effect free; do not add try/catch blocks or telemetry activities inside update definition methods."
---

# NTS MongoDB

Use MongoDB.Driver APIs in the simplest typed form that fits the operation. Keep Mongo persistence code focused on persistence concerns and leave domain decisions to Domain or Application code.

## Update Definitions

When implementing methods such as `GetUpdateDefinition`:

- Return the `Builders<T>.Update` definition directly.
- Use typed member expressions such as `.Set(x => x.Name, document.Name)` where possible.
- Do not start telemetry activities inside update definition methods.
- Do not wrap update definition construction in `try/catch`.
- Do not tag and rethrow exceptions from update definition builders.
- Keep the method side-effect free: no I/O, logging, telemetry, repository calls, or domain decisions.

If telemetry or exception tagging is needed, place it around the Mongo operation that executes the update, not around construction of the update definition.

## Repository Boundaries

Repositories may translate models to Mongo filters, updates, and collection operations. They should not enforce domain invariants or coordinate workflows across repositories. Move those rules to the owning Domain object or Application service.
