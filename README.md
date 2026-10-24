# Not a Timing System (NTS)
Integrated app to create, configure and run your Endurance category equestrian sport events. NTS is the improved version of [EMS](https://github.com/Not-Endurance/endurance-management-system) which was started in 2018 on WPF

# Running the app
NoTiming is one web platform. `NoTiming.Api`, an ASP.NET Core host, serves `NoTiming.Ui`, a Blazor WebAssembly app, and the live hub on one origin (ADR-0011). The legacy Azure Functions API (`NTS.Nexus.HTTP`) still serves the data until its routes are ported and it is retired.

## Prerequisites
- .NET 10 SDK. The shared libraries still target .NET 8 and build with it; the Functions API runs on the .NET 8 runtime.
- Docker, for the Functions API's MongoDB and Azurite.

## Debug
1. Start the legacy API and its storage (execute in the project root):
   ```
   docker compose -f docker/nexus.yaml up -d
   ```
2. Run the Api, which serves the Ui:
   ```
   dotnet run --project src/NoTiming.Api --launch-profile Development
   ```
3. Open http://localhost:11337

## Publish
`dotnet publish src/NoTiming.Api -c Release` also publishes the Ui and ships it as the Api's `wwwroot`. Pass `-p:WasmApplicationEnvironmentName=Staging` (or `Production`): .NET 10 bakes the Blazor environment into the build, and without it the Ui loads the Production settings.

### Diagrams
- A reasonably-updated [Domain Model diagram](https://github.com/Not-Endurance/not-timing-system/blob/develop/diagrams/NTS%20v2%20Domain%20Model.drawio.png)
- [Appflow diagram](https://github.com/Not-Endurance/not-timing-system/blob/develop/diagrams/NTS%20v2%20Appflow.drawio.png)

### Project structure
Read [CONTEXT.md](CONTEXT.md) for the language of the domain and `docs/adr` for the decisions behind the layout.
- `src/NoTiming.Api` is the host: the live hub, health, security headers, and the Ui.
- `src/NoTiming.Ui` is the Blazor WebAssembly app: the pages, layout, view services and the REST repositories.
- `src/Domain` holds the domain model in three boundaries: `NTS.Domain` (shared), `NTS.Domain.Setup` (create and configure an Event) and `NTS.Domain.Core` (run the Event).
- `src/NTS.Contracts` holds the models and interfaces both hosts share. `src/NTS` holds the localization.
- `src/NTS.Application` is the application layer the Functions API still uses; it moves into the hosts when that API is retired.
- `src/Apps/Nexus/NTS.Nexus.HTTP` is the legacy Azure Functions API.
- `nugets` holds the `Not.*` libraries: generic infrastructure and components, free of business logic.
- `tests` holds the unit tests and the integration tests (see `tests/NTS.Tests.Integration/README.md`).
