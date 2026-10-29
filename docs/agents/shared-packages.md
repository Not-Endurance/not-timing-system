# Shared packages and NoTiming

Code that is not NoTiming business logic goes into the `Not*` package of its layer, under `nugets/` (AGENTS.md, rule 11). What
stays in `src/` and `tools/` is what NoTiming does for timing and sport: its Events, Participants, Rankings and Snapshots, the
rules of their stages, and the screens and endpoints that show them.

## The test

Ask whether another product on the same platform would carry this code unchanged, with no NoTiming word in it. If it would, it
is shared, and it goes to the package of its layer. If it names a NoTiming concept, it stays. Code that is generic but was
written for a NoTiming screen is generalised first: the names of NoTiming come out, and what varies is passed in. Then it moves.

## Where it goes

| Package | Holds |
|---|---|
| `Not` | Generic .NET helpers: strings, collections, dates, reflection, serialization, logging, localisation, extensions. |
| `Not.Domain` | Abstractions of the domain layer: the base types and exceptions a domain uses. |
| `Not.Application` | Application patterns: CRUD and JSON:API answers, HTTP and RPC helpers, caching, configuration, environments, services. |
| `Not.Krud` | The CRUD framework: its abstractions, models, graph, services, their registration, and its Blazor side. |
| `Not.Identity` | Sign-in and identity plumbing: sessions, codes, email senders, the Mongo stores. |
| `Not.Storage` | Storage adapters: Mongo and REST. |
| `Not.Blazor` | Blazor components, dialogs, helpers and navigation. |
| `Not.Blazor.Client` | Browser-side helpers for the Blazor client. |
| `Not.Test` | Test support any test project can use: the deadline wait, the journey runner, test ids, the repository root. |

A piece of test support that only one NoTiming project needs stays in that project's `Harness` folder (AGENTS.md, rule 7).

## Examples

- A formatter for race times: `Not`.
- A retry policy for HTTP calls: `Not.Application`.
- A confirmation dialog: `Not.Blazor`.
- A table with paging, sorting and filtering for any list of rows: `Not.Krud`.
- The rule that a Ranking is finalised after its last day: NoTiming (`src/`).

## Keeping them shared

- A `Not*` package names no NoTiming type and references no NoTiming project. The dependency runs one way: NoTiming references the
  `Not*` packages, never the reverse.
- Namespaces start with the package's name, as the code in each package does.
- Nothing enforces this beyond AGENTS.md, rule 11, and this guide. An architecture test that fails a `Not*` project referencing a
  NoTiming project would enforce it; that test is not written yet.
