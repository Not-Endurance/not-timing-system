# The API is JSON:API-shaped, filters use the OData grammar, and each resource moves as it is ported

Status: proposed

Every route of the Functions API returns a `Result<T>` envelope, turns a domain error into HTTP 200 with a message and no code, answers a missing item with 200 and null, and accepts `$filter` on only nine of its resources. Clients swallow non-2xx responses, so they cannot tell one failure from another. ADR-0003 moves the Judge-facing surface into NoTiming.Api, and the issues for that work asked for the same paths and payloads. We drop that requirement: the functionality must survive, the routes and payloads need not. NoTiming.Api follows JSON:API, with real status codes and errors that carry a stable code. The `filter` parameter carries an OData expression, the existing OData package applies it to Mongo, and `sort` and `page[...]` map onto the same package's options. Each resource is ported JSON:API-native, and its client repository switches in the same ticket. The rules live in `.claude/skills/rest-api/SKILL.md`, and an ADR that adds or changes an endpoint links to it instead of restating them.

## What follows from it

**Resources move one at a time.** The Functions project serves whatever has not moved. ADR-0003 still decides the host and the anonymous route group; ADR-0007 needs events, participations, handouts, rankings and snapshot results first.

**The client gets a JSON:API variant.** `Not.Storage`'s `ApiRepository` grows a variant that a resource opts into. `Result<T>` disappears per resource as it moves, and the client's request handling reads the error `code` instead of swallowing the failure.

**One grammar for filtering.** `filter=eventId eq 5` is parsed by `Microsoft.AspNetCore.OData` and applied to Mongo LINQ, as `$filter` is today. The client's `ODataApiFilterAdapter` stays; it silently fetches everything when it meets an expression it cannot translate and has no tests, so each resource that depends on it gets tests first.

**Domain errors become statuses.** A rule violation is 422, a conflict with the current state (`event-ended`) is 409, a malformed request or filter is 400, a missing item is 404, each with an error `code`.

## Considered options

**Port the routes and payloads as they are.** Easier to cut over, but it carries HTTP 200 for errors and the absence of error codes into the new host and pins them with parity tests.

**One step for all 92 routes and every client.** Too much to change and release at once.

**`$filter` as the parameter name.** JSON:API reserves `filter` for filtering, and a parameter with a `$` is only allowed for implementation-specific use.

**Full OData.** It brings its own `$`-options, metadata and payload model. Only its filter grammar is borrowed.

**A JSON:API library.** The ones we know of target EF Core, and Mongo translation already comes from the OData package.

## Consequences

Two conventions coexist until the last resource moves. The skill lists the legacy resources, and each porting ticket removes its own from that list.

Installed Judge builds and Witness need a release per resource cutover, as ADR-0003 already requires for the address change.

Translating a comparison on a `DateTimeOffset` (and OData's `now()`) to Mongo is unverified on this path. The first ticket that depends on it starts with a spike.
