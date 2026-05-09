# NTS.Tests.Integration

These tests start real local infrastructure and app processes. They live in a separate project so normal unit-test runs do not require Docker.

## Run Locally

```powershell
dotnet test .\tests\NTS.Tests.Integration\NTS.Tests.Integration.csproj -c Debug
```

Requirements:

- Docker must be running.
- Azure Functions Core Tools v4 must be installed and `func` must be on `PATH`.
- Azurite must be running for the Functions host storage connection (`UseDevelopmentStorage=true`).
- The test harness starts a MongoDB container with Testcontainers.
- The test harness starts `NTS.Nexus.HTTP` as a child `func start` process on a random localhost port.
- The test harness starts `NTS.Nexus.Warp` as a child `dotnet run --no-build` process on a random localhost port.

## Current Coverage

- Boots MongoDB, real Nexus HTTP, and real Warp.
- Seeds events, participations, officials, and users through the actual Nexus HTTP API.
- Builds one real Judge application service provider and two real Witness application service providers.
- Wires Judge and Witness to the same REST storage registration used by the apps.
- Connects all three app instances to the same event through their real `INtsSocketService` implementations.
- Drives Judge through `ISnapshotService.Record`, so phase completion is produced by the Judge domain/application layer.
- Verifies both Witness application instances update their real participation state through `WitnessRpcClient` and domain-event handlers.
- Verifies one Witness resolves official access while the other resolves participant/default access.
- Reads the persisted participation and snapshot results back through the Nexus HTTP API.

## Next Expansion

- Add snapshot submission and pending-snapshot flush scenarios.
- Add a thin Playwright smoke suite for browser-only behavior.
