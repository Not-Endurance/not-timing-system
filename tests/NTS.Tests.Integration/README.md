# NTS.Tests.Integration

These tests start real local infrastructure and the real hosts. They live in a separate project so normal unit-test runs do not require Docker.

## Run Locally

```powershell
dotnet test .\tests\NTS.Tests.Integration\NTS.Tests.Integration.csproj -c Debug
```

Four kinds of test run in the project:

- The Api tests (`ApiHostTests`, `SignInTests`, `IdentityStoreTests`, `PasskeyStoreTests`, `PasskeyServiceTests`, `PasskeyEndpointTests`) host `NoTiming.Api`, or its identity stores, in this process on a MongoDB in a container. They need Docker and nothing else.
- The passkey ceremonies (`PasskeyCeremonyTests`) drive a real Chromium against the Api on a real port, and need Docker and the browser (see Browser Setup).
- The tests that need no infrastructure at all (`IdentifierShapeTests`, `AccountTextTests`, `NameRenderingTests`) run anywhere the solution builds.
- The scenarios that use the legacy Functions API (everything driven through `NexusApiDriver` and `ClientDriver`) need Docker, Azure Functions Core Tools and Azurite.

Requirements for the scenarios:

- Docker must be running. The harness starts a MongoDB container with Testcontainers.
- Azure Functions Core Tools v4 must be installed and `func` must be on `PATH`. The harness starts `NTS.Nexus.HTTP` as a child `func start` process on a random localhost port.
- Azurite must be running for the Functions host storage connection (`UseDevelopmentStorage=true`).
- .NET 10 SDK, and the .NET 8 runtime for the Functions worker.

The Functions API, Azurite and Node leave the harness when that API is retired (#647).

## The harness

- `ApiFactory` is `NoTiming.Api` in this process, built on `WebApplicationFactory<Program>`, on the MongoDB the test gives it. By default a test reaches it in memory (at an https address, so the Secure session cookie is one a browser would take). In Kestrel mode (`UseKestrel(0)`, a free port of its own) it listens on a real loopback port, for the clients that need one: the SignalR client of the Ui, WebSockets and browsers. `ApiHostFixture` starts it in Kestrel mode with its own MongoDB, and so does `NtsIntegrationFixture`, next to its MongoDB container and the Functions process; `MongoFixture` is just the container, for tests that build their own host or none.
- Every host the harness builds has its own data protection key ring under the test output, never in the user profile, and sends its email to an outbox. A test passes the clock it moves (`FakeTimeProvider`) for the lifetimes of codes and sessions, and a key ring folder to share when a second host must read what the first protected: that is how a restart is tested.
- `ClientDriver` is the client: it builds the Ui's real service provider (the REST repositories and the live-connection client) against the Api and the Functions API, and drives it as an anonymous visitor or as a signed-in user. `NexusApiDriver` talks to the Functions API directly, to seed and read data.
- Sign-in in a test is the way a person signs in (ADR-0002). `ApiSessions.SignInAsync` asks for a code, reads it from the outbox of the host (`IEmailOutbox`), sends it back and returns the session cookie, which the test carries by hand so it works for an in-memory client and for one on a real port alike (a Secure cookie of a plain-http address would not be sent back). `UserSeed` adds users as they exist before identity: a row of the Functions API with the profile fields and none of the identity ones. The host has no sign-in as, no test scheme and no test path: `ApiHostTests` asserts that the session cookie is its only scheme and that a bearer token of the old `integration|...` format signs nobody in.

## Browser Setup

The passkey ceremonies (`PasskeyCeremonyTests`) run Chromium with a virtual authenticator against the real pages of the Api, so they need the browser; so do the parked print scenarios (below), which call the PDF routes of the Functions API. Use one browser cache per operating system. For example, after building the test project:

```powershell
$env:PLAYWRIGHT_BROWSERS_PATH = "$PWD\.tmp\ms-playwright"
pwsh .\tests\NTS.Tests.Integration\bin\Debug\net10.0\playwright.ps1 install chromium
```

The conditional UI of the email field (the autofill list of passkeys) cannot be automated: the tests sign in through the button, which runs the same routes through the modal prompt, and the autofill on iOS Safari and Android Chrome goes on the manual device checklist in the go/no-go report of #600.

The harness uses `NTS_INTEGRATION_PLAYWRIGHT_BROWSERS_PATH` when set, otherwise it keeps a valid `PLAYWRIGHT_BROWSERS_PATH`, then probes `/ms-playwright`, `.tools/ms-playwright`, and `.tmp/ms-playwright` for a browser matching the current OS. The Nexus HTTP container image uses `/ms-playwright`.

## Current Coverage

- The Api: health, the security headers, deep links into the Ui and the JSON 404 of an unknown API route, HTTPS redirection and HSTS outside Development, the live hub (an anonymous client joins an Event's group and receives only that Event's change notifications, no method is callable by a client, a connection that names no Event, or something that is not an Event id, is refused, the WebSocket origin check, CORS), and the session cookie as the only way in.
- Signing in with an emailed code (`SignInTests`): the answer to a code request is the same for every address, a code works once, expires after ten minutes, is invalidated by five wrong attempts and replaced by a new request after the resend cooldown; the session cookie, `/api/me`, signing out, deleting a ticket, rotating the security stamp, a host restart, a user from before identity, the Production refusal of the console and outbox senders, the page text in en, bg and tr, and the media type every write needs.
- Passkeys (ADR-0002, #600): the store (`PasskeyStoreTests`: a credential read back is the credential that was stored, three sign-ins leave one entry, passkeys enrolled at the same moment all persist, refusal when an update is not retried, removal, the unique index, the fields of the application untouched); the routes (`PasskeyEndpointTests`: what each requires and refuses, the options an authenticator is asked for, the origin rule, the antiforgery cookie per environment, adding, listing, renaming and removing over HTTP with a `SoftwareAuthenticator` that makes a passkey without a browser, the limit on a name, someone else's passkey, a mail that cannot be sent, a return path with a control character); the service (`PasskeyServiceTests`: a passkey removed while it was signing in is not added back, and a rename made meanwhile is kept); and the ceremonies in a real browser (`PasskeyCeremonyTests`: code sign-in, the offer, adding a passkey, signing out and back in with it, three sign-ins leaving one entry with the authenticator's sign count, two devices adding at the same moment, a removed passkey refused, the last one kept, the stamp ending a passkey session, the second-passkey offer, the mail in en, bg and tr).
- The identity user store on a MongoDB (`IdentityStoreTests`): a row from before identity read and updated field for field, every identity field round-tripped, capitalisation, concurrent updates, stamp rotation and the partial unique indexes.
- The change notification (#623): the Api announces a change with one notification to the clients of that Event and to no other (`ParticipationChangesTests`), and two Ui viewers on the real hub, with the real store and the Functions API behind it, show a Participation that appeared once the Api announced it and not before (`ViewersFollowChangesTests`).
- Rankings and Handouts hold references (#624, ADR-0006): a stored Ranking holds, per entry, the id of a Participation, the not-ranked mark and the stored rank, and a stored Handout holds its id, its Event's id and the id of its Participation; neither holds a Participation. The Handouts of a Participation are read by a filter the server applies, from the Ui's own repository (`RankingAndHandoutStorageTests`). A default (a mark that is false, a rank that is null) is not written to MongoDB (`NtsMongoSerialization`).
- Every id is a Guid, stored as a standard UUID (`IdentifierShapeTests`, `GuidStorageTests`).
- Witness registration resolution, profile completion, and sign-in completing after startup, against the Functions API with the Ui's real services.

## Parked scenarios

The Judge app is gone (#598, ADR-0011), and with it the connected Judge that these scenarios were written around. The hub no longer accepts Snapshots either (ADR-0013). They stay in the repository, unchanged and not compiled (`Compile Remove` in `NTS.Tests.Integration.csproj`), until the owner approves what each becomes (AGENTS.md rule 4, #642):

- `IntegrationHarnessCheckTest.Parked.cs`: `Judge_snapshot_flow_updates_connected_witness_applications`, `Judge_handouts_follow_phase_completion_rules_and_snapshot_keeps_selection`, `Presentlist_updates_from_judge_events_on_every_connected_witness` (a connected Judge), `Witness_snapshot_selections_restore_from_user_session_until_published`, `Operators_are_projected_and_gate_witness_write_access` (they publish through the hub write path)
- `JudgeDependencyInjectionTests.cs`
- `FeiExportTests.cs` (the FEI export lived in the Judge app)
- `EndToEndEventTests/` (the event replays and the compulsory-inspection setup test, driven through the Judge)
- `Drivers/JudgeDriver.cs`

Since #623 the five events of arrival, elimination, restoration, required inspection and required representation, their base and `IWitnessClientProcedures` are gone: a viewer is told only that a Participation changed (`ParticipationChanged`), and `PhaseCompleted` carries ids and a final flag and stays on the server. A scenario that waits for one of the old events waits for something that no longer exists; `JudgeDriver` and `EndToEndEventTests` are built around them.

Since #624 an entry of a Ranking is a Participation id with its not-ranked mark and stored rank, and a Handout is a Participation id with its own id and Event id (ADR-0006). The Results and Handout documents are composed from them and the Event's Participations (`new Result(ranking, participations)`, `new Result(handout, participation)`), so the parked scenarios that read `entry.Participation` or `handout.Entries`, or build an entry from a Participation (`FeiExportTests`, `EndToEndEventTests/`, `HandoutsForNumber` of `IntegrationHarnessCheckTest.Parked.cs`), compose the document first when they come back. The golden `EndToEndEventTests/Snapshots/*/nts.event_rankings.json` is converted to the stored shape (entries of `ParticipationId`, `Rank` and `IsNotRanked`; the ids stay the integers of the other files in the folder until #642 regenerates them all as Guids); `SnapshotJson.ReplaceIds` and `EndToEndEventSnapshot.CreateIdMap` have to map `ParticipationId` too when they run again.

Since #622 the Ui keeps the Event's Participations in one store (`IParticipationStore`), and every list and page is a view over it. Un-parking a scenario that looks at them means reading the store: `IParticipationContext.Participations` is only the view of those still to be timed, so a completed Participation is not in it, `ClientDriver.WaitForParticipation` reads that view, and `IntegrationHarnessCheckTest.Parked.cs` still names `IPerformanceParticipations`, which is gone.

## Next Expansion

- More browser coverage for the Ui pages as they arrive, on the same Chromium kit as the passkey ceremonies.
- Snapshot POST scenarios once the server records them (#644), which replace the parked ones.
