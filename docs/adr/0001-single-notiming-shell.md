# 0001: One NoTiming shell over Api, EventBus and Ui

## Status

Accepted.

## Context

The server was one host, Nexus Warp, that served the REST API, the SignalR hubs and the Witness Blazor UI. Its files were spread over several projects with inconsistent names. Judge is a separate MAUI app and talks to the server over REST and SignalR.

## Decision

- **NoTiming** is the only deployable server. It is a single ASP.NET Core host, deployed as one Azure Web App. It composes three capabilities: **Api** (REST), **EventBus** (SignalR) and **Ui** (the Blazor WebAssembly client it serves).
- **EventBus** holds the hubs, connection tracking and hub-level plumbing. It holds no business logic. Its purpose is to relay realtime notifications.
- **Judge** stays a separate MAUI application and is not part of the shell.
- Client-facing addresses are frozen: REST routes under `/api`, the hub paths `/event-bus/judge` and `/event-bus/witness`, the UI under `/ui`, and `/healthz`.

## Consequences

- EventBus depends on Api.Application for buffered snapshots and receive authorization. Api depends on Ui.Contracts for the register and profile payloads. Both references were kept deliberately for the rebrand and are tracked in issue #688, which will decide how to remove them.
- Witness is kept only where a client-facing name depends on it: the `event-bus/witness` path, the `WITNESS_HUB` constant, `WitnessRpcHub` (named after that path), the `WITNESS_UI_PATH` variable and client-visible messages. Everything else is named Ui.
- The shell's assembly is named `NoTiming.Shell`. The convention scanner in `nugets` only follows referenced assemblies whose names contain a dot, so a dotless `NoTiming` assembly would drop the NoTiming services from registration. `nugets` is not changed to work around this.
- Azure resources, publish-profile secret names, user-secrets ids (`nts-warp-{env}`) and workflow file names are unchanged.
