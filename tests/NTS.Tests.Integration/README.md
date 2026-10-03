# NTS.Tests.Integration

These tests start real local infrastructure and the real hosts. They live in a separate project so normal unit-test runs do not require Docker.

## Run Locally

```powershell
dotnet test .\tests\NTS.Tests.Integration\NTS.Tests.Integration.csproj -c Debug
```

Three kinds of test run in the project:

- The Api tests (`ApiHostTests`, `SignInTests`, `IdentityStoreTests`) host `NoTiming.Api`, or its identity stores, in this process on a MongoDB in a container. They need Docker and nothing else.
- The tests that need no infrastructure at all (`IdentifierShapeTests`, `AccountTextTests`, `NameRenderingTests`) run anywhere the solution builds.
- The scenarios that use the legacy Functions API (everything driven through `NexusApiDriver` and `ClientDriver`) need Docker, Azure Functions Core Tools and Azurite.

Requirements for the scenarios:

- Docker must be running. The harness starts a MongoDB container with Testcontainers.
- Azure Functions Core Tools v4 must be installed and `func` must be on `PATH`. The harness starts `NTS.Nexus.HTTP` as a child `func start` process on a random localhost port.
- Azurite must be running for the Functions host storage connection (`UseDevelopmentStorage=true`).
- .NET 10 SDK, and the .NET 8 runtime for the Functions worker.

The Functions API, Azurite and Node leave the harness when that API is retired (#647).

## The harness

- `ApiFactory` is `NoTiming.Api` in this process, built on `WebApplicationFactory<Program>`, on the MongoDB the test gives it. By default a test reaches it in memory (at an https address, so the Secure session cookie is one a browser would take). In Kestrel mode (`UseKestrel(0)`, a free port of its own) it listens on a real loopback port, for the clients that need one: the SignalR client of the Ui, WebSockets and, later, browsers. `ApiHostFixture` starts it in Kestrel mode with its own MongoDB, and so does `NtsIntegrationFixture`, next to its MongoDB container and the Functions process; `MongoFixture` is just the container, for tests that build their own host or none.
- Every host the harness builds has its own data protection key ring under the test output, never in the user profile, and sends its email to an outbox. A test passes the clock it moves (`FakeTimeProvider`) for the lifetimes of codes and sessions, and a key ring folder to share when a second host must read what the first protected: that is how a restart is tested.
- `ClientDriver` is the client: it builds the Ui's real service provider (the REST repositories and the live-connection client) against the Api and the Functions API, and drives it as an anonymous visitor or as a signed-in user. `NexusApiDriver` talks to the Functions API directly, to seed and read data.
- Sign-in in a test is the way a person signs in (ADR-0002). `ApiSessions.SignInAsync` asks for a code, reads it from the outbox of the host (`IEmailOutbox`), sends it back and returns the session cookie, which the test carries by hand so it works for an in-memory client and for one on a real port alike (a Secure cookie of a plain-http address would not be sent back). `UserSeed` adds users as they exist before identity: a row of the Functions API with the profile fields and none of the identity ones. The host has no sign-in as, no test scheme and no test path: `ApiHostTests` asserts that the session cookie is its only scheme and that a bearer token of the old `integration|...` format signs nobody in.

## PDF Browser Setup

Only the parked print scenarios (below) call the PDF routes of the Functions API, and they need Chromium. Use one browser cache per operating system. For example:

```powershell
$env:PLAYWRIGHT_BROWSERS_PATH = "$PWD\.tmp\ms-playwright"
pwsh .\src\Apps\Nexus\NTS.Nexus.HTTP\bin\Debug\net8.0\playwright.ps1 install chromium
```

The harness uses `NTS_INTEGRATION_PLAYWRIGHT_BROWSERS_PATH` when set, otherwise it keeps a valid `PLAYWRIGHT_BROWSERS_PATH`, then probes `/ms-playwright`, `.tools/ms-playwright`, and `.tmp/ms-playwright` for a browser matching the current OS. The Nexus HTTP container image uses `/ms-playwright`.

## Current Coverage

- The Api: health, the security headers, deep links into the Ui and the JSON 404 of an unknown API route, HTTPS redirection and HSTS outside Development, the live hub (an anonymous client joins an Event's group and receives only that Event's change notifications, no method is callable by a client, a connection that names no Event, or something that is not an Event id, is refused, the WebSocket origin check, CORS), and the session cookie as the only way in.
- Signing in with an emailed code (`SignInTests`): the answer to a code request is the same for every address, a code works once, expires after ten minutes, is invalidated by five wrong attempts and replaced by a new request after the resend cooldown; the session cookie, `/api/me`, signing out, deleting a ticket, rotating the security stamp, a host restart, a user from before identity, the Production refusal of the console and outbox senders, the page text in en, bg and tr, and the media type every write needs.
- The identity user store on a MongoDB (`IdentityStoreTests`): a row from before identity read and updated field for field, every identity field round-tripped, capitalisation, concurrent updates, stamp rotation and the partial unique indexes.
- Every id is a Guid, stored as a standard UUID (`IdentifierShapeTests`, `GuidStorageTests`).
- Witness registration resolution, profile completion, and sign-in completing after startup, against the Functions API with the Ui's real services.

## Parked scenarios

The Judge app is gone (#598, ADR-0011), and with it the connected Judge that these scenarios were written around. The hub no longer accepts Snapshots either (ADR-0013). They stay in the repository, unchanged and not compiled (`Compile Remove` in `NTS.Tests.Integration.csproj`), until the owner approves what each becomes (AGENTS.md rule 4, #642):

- `IntegrationHarnessCheckTest.Parked.cs`: `Judge_snapshot_flow_updates_connected_witness_applications`, `Judge_handouts_follow_phase_completion_rules_and_snapshot_keeps_selection`, `Presentlist_updates_from_judge_events_on_every_connected_witness` (a connected Judge), `Witness_snapshot_selections_restore_from_user_session_until_published`, `Operators_are_projected_and_gate_witness_write_access` (they publish through the hub write path)
- `JudgeDependencyInjectionTests.cs`
- `FeiExportTests.cs` (the FEI export lived in the Judge app)
- `EndToEndEventTests/` (the event replays and the compulsory-inspection setup test, driven through the Judge)
- `Drivers/JudgeDriver.cs`

## Next Expansion

- A thin Playwright smoke suite for browser-only behavior, against the Kestrel-hosted Api.
- Snapshot POST scenarios once the server records them (#644), which replace the parked ones.
