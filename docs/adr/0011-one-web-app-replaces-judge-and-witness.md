# One web app replaces Judge and Witness, and the API is its only door

Status: proposed

Judge is a Windows executable (MAUI Blazor Hybrid) with no sign-in, Witness is a Blazor WebAssembly app behind MSAL, and behind them sit an Azure Functions REST API and a separate SignalR host. The two apps share most of their code through six projects, but each has its own shell, navigation, installer or hosting, release and localization, and the Judge surface reaches the backend anonymously (ADR-0003). We decided that NoTiming is one product: `NoTiming.Api`, one ASP.NET Core host that serves REST, sign-in, the hub and the UI, and `NoTiming.Ui`, one Blazor WebAssembly app with a Console for the Main Operator, a Snapshot page for Staff and the public viewer pages. Judge and Witness, along with MAUI, Azure Functions, Warp, Nexus and Static Web Apps, are retired from the code and from the product. Every project moves to .NET 10. The API is the only door to the data: every route requires a signed-in caller unless it belongs to the explicit public-read allowlist (ADR-0001, ADR-0012).

## What follows from it

**Two projects, one origin.** `NoTiming.Api` hosts `NoTiming.Ui` and serves the sign-in pages as static server-rendered pages (passkeys and cookie session, ADR-0002); everything else runs in the browser. There is no CORS and no second host to share a key ring with. The Api references the Ui to serve it and the Ui never references the Api, so the project graph keeps them apart and they can be deployed separately later.

**The Console runs in WebAssembly too.** Judge is already a thin client over REST and a hub, so it moves almost as it is and its state is a cache of what the server stores. The Console's pages load on demand, so a phone that only follows an Event does not download them.

**Seven projects instead of twenty on the NTS side.** `NTS` (localization), `NTS.Domain` (shared, now with Tenant and the account), `NTS.Domain.Setup`, `NTS.Domain.Core` (which absorbs `NTS.Domain.Watcher`), `NTS.Contracts` (the four contracts projects merged) and the two hosts. `NTS.Application`, `NTS.Storage` and `NTS.Client` go: the view services and REST repositories move into the Ui, the server logic and the Mongo wiring into the Api, and tests reference the Ui project directly. The client repositories move at the foundation; `NTS.Application` stays a library until the Functions API retires, because that API still uses it. The layering that those assemblies enforced is kept as folders in the Api and by the project graph. Among the `Not.*` libraries, `Not.MAUI` and the empty `Not.Tests` are deleted, `Not.Blazor.Client` folds into `Not.Blazor`, and `Not.Server` loses the Playwright renderer, the JWT validation and OpenID Connect and stays as the home of the identity library of ADR-0002. The `NTS.*` prefix stays for the surviving libraries.

**.NET 10 everywhere, in two steps.** ADR-0002 moved only the leaf apps because MAUI Judge consumed the shared libraries. Judge leaves first, so the hosts and the tests move to .NET 10 at once. The Functions API is then the last consumer holding the libraries on .NET 8: managed Functions cannot run on .NET 10 (ADR-0003) and a .NET 8 project cannot reference .NET 10 ones, so the libraries move in one pass when that API is retired. .NET 8 and 9 leave support on 10 November 2026.

**Browser print only.** The PDF and ZIP print endpoints and the Playwright renderer are deleted; the Console prints through the browser, which Judge already does in production.

**One cutover.** The MAUI Judge has no login, so it can never call an authorized API. It keeps using the legacy anonymous Functions API until a single cutover that applies every data migration, swaps in the new hosts and retires the Functions API and the desktop executable together. No installed build needs a new address, which removes the cutover cost ADR-0003 accepted. The exposure of the legacy API stays what it is today until then (#593).

**Tests host the Api in-process.** The integration fixture starts the Api with `WebApplicationFactory`, the Judge and Witness drivers merge into one, and `func`, Azurite, Node and Playwright leave the suite and its workflow. The environment-gated test-token path in the production host is deleted; tests authenticate through a scheme registered in the test project.

## Considered options

**Keep two apps and share more.** Still two shells, two navigations and two sets of localized text, and Judge stays outside authorization.

**Blazor Server for the Console, WebAssembly for everything else.** Shared views then need two data paths (in-process for Judge, HTTP for Witness) or Judge calls its own API over loopback, which means carrying the user's cookie into a circuit. A deploy kills every circuit. Judge is already a thin client, so WebAssembly is the smaller change. The door stays open: `IRepository<T>` is the seam, and the Console could move to a server circuit later if a concrete pain shows up.

**Blazor Server for everything.** A circuit per viewer on phones that background constantly, which is the failure ADR-0002 cites against MSAL, and it makes the JSON:API layer serve nobody.

**Interactive Auto.** The first visit of every phone opens a circuit, which is the load that cannot be predicted, and component state resets when it hands over to WebAssembly.

**Keep `NTS.Application`, `NTS.Storage` and `NTS.Client`.** Each would have one consumer; the boundary they enforce costs more than it protects here.

**Keep Functions, or a Function App.** Rejected in ADR-0003 for the same reasons: runtime support, no static address for the Atlas access list, a second deployable.

## Consequences

ADR-0003 is superseded: no anonymous Judge route group is ever built. ADR-0002 is amended: the .NET scope, and the reason the hubs must share a process (it was Judge's in-memory connection, ADR-0013). REST and the hub stay in one process and single-instance until a hub backplane exists.

Phones that follow an Event download the shared Ui bundle; a spike sizes it with the Console loaded on demand. #645 measured it: 8.29 MB as Brotli over 180 files for the viewers' app, which a test holds under 9 MB, and the Console's pages are kept out of it by a seam (the first segment of a path names the assemblies fetched when a person goes there) that the Console ticket fills. Ranking, FEI export and Results rendering run in WebAssembly instead of natively, on Event-sized data; any step that proves slow moves behind the API. A Console tab survives a server restart, because its state is in the browser and the hub reconnects.

Judge and Witness stop existing as names: the remaining wording in ADRs, the glossary, routes, resource strings and the integration tests follows `CONTEXT.md` (Console, Snapshot), one ticket at a time.

Any endpoint this decision adds or changes follows `.claude/skills/rest-api/SKILL.md` (ADR-0008).
