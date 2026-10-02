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
- Chromium must be installed for the Nexus HTTP PDF backend. The harness uses `NTS_INTEGRATION_PLAYWRIGHT_BROWSERS_PATH` when set, otherwise it keeps a valid `PLAYWRIGHT_BROWSERS_PATH`, then probes `/ms-playwright`, `.tools/ms-playwright`, and `.tmp/ms-playwright` for a browser matching the current OS.
- The test harness starts a MongoDB container with Testcontainers.
- The test harness starts `NTS.Nexus.HTTP` as a child `func start` process on a random localhost port.
- The test harness starts `NoTiming.Api` as a child `dotnet run --no-build` process on a random localhost port.

## PDF Browser Setup

Use one browser cache per operating system. For example:

```powershell
$env:PLAYWRIGHT_BROWSERS_PATH = "$PWD\.tmp\ms-playwright"
pwsh .\src\Apps\Nexus\NTS.Nexus.HTTP\bin\Debug\net8.0\playwright.ps1 install chromium
dotnet test .\tests\NTS.Tests.Integration\NTS.Tests.Integration.csproj -c Debug
```

```bash
export PLAYWRIGHT_BROWSERS_PATH="$PWD/.tools/ms-playwright"
pwsh ./src/Apps/Nexus/NTS.Nexus.HTTP/bin/Debug/net8.0/playwright.ps1 install chromium
dotnet test ./tests/NTS.Tests.Integration/NTS.Tests.Integration.csproj -c Debug
```

On WSL/Linux, Chromium also needs native OS packages. If the PDF step fails with a missing shared library such as `libnspr4.so`, run `pwsh ./src/Apps/Nexus/NTS.Nexus.HTTP/bin/Debug/net8.0/playwright.ps1 install-deps chromium` once, or run the suite in a container image that already includes those dependencies.

The Nexus HTTP container image uses `/ms-playwright`; keep `PLAYWRIGHT_BROWSERS_PATH=/ms-playwright` for container runs.

## Current Coverage

- Boots MongoDB, real Nexus HTTP, and the real Api (`NoTiming.Api`).
- Seeds events, participations, officials, and users through the actual Nexus HTTP API.
- Builds real Witness application service providers wired to the same REST storage registration the app uses.
- Verifies Witness registration resolution, profile completion, and sign-in completing after startup against the Nexus HTTP API.

## Parked scenarios

The Judge app is gone (#598, ADR-0011), and with it the connected Judge that these scenarios were written around. The hub no longer accepts Snapshots either (ADR-0013). They stay in the repository, unchanged and not compiled (`Compile Remove` in `NTS.Tests.Integration.csproj`), until the owner approves what each becomes (AGENTS.md rule 4, #642):

- `IntegrationHarnessCheckTest.Parked.cs`: `Judge_snapshot_flow_updates_connected_witness_applications`, `Judge_handouts_follow_phase_completion_rules_and_snapshot_keeps_selection`, `Presentlist_updates_from_judge_events_on_every_connected_witness` (a connected Judge), `Witness_snapshot_selections_restore_from_user_session_until_published`, `Operators_are_projected_and_gate_witness_write_access` (they publish through the hub write path)
- `JudgeDependencyInjectionTests.cs`
- `FeiExportTests.cs` (the FEI export lived in the Judge app)
- `EndToEndEventTests/` (the event replays and the compulsory-inspection setup test, driven through the Judge)
- `Drivers/JudgeDriver.cs`

## Next Expansion

- Rebuild the harness around the in-process Api (#642).
- Add a thin Playwright smoke suite for browser-only behavior.
