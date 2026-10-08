## Authentication - the session of the host (ADR-0002)

`NoTiming.Ui` is a client-side Blazor WebAssembly app that the Api serves (ADR-0011). It holds no token and no sign-in
state of its own, and has no setting for signing in: the session is the `__Host-NoTiming` cookie that the Api sets, which
the browser sends to the page's own origin and the app cannot read. The app asks the Api who is signed in
(`GET /api/me`, `IAccountSession`) when it starts and when it is told to ask again.

- Signing in is a full-page navigation to the Api's `/sign-in?returnUrl=...`, which returns the person to the page they
  were on (only a path of this site is passed on). Signing out is `DELETE /api/sessions/current`, followed by loading
  the app again as a visitor.
- What a page asks of a person is decided in one place (`WitnessRoutePolicy`, applied by `AccountRouteGate`): everything
  that only shows is public, sending Snapshots and the profile need a person who is signed in, and sending Snapshots also
  waits for a complete profile. The Api decides what is allowed whatever a page shows (ADR-0012).
- What depends on who is signed in (`AccountAwareContext`: the profile, the access level) loads again when the account
  changes. What a person keeps per Event (the selected and the sent Snapshots) is kept by the Api under their account
  (`/api/user-sessions`), not in the browser.

## One origin (ADR-0011)

The app, the resources (`/api`) and the live hub (`/live-hub`) are the Api's, so the app reaches them where it came
from: `Program.cs` sets `RpcSettings:Host` to the page's own address whatever a settings file says, and the session
cookie is sent to that origin only. There is no setting for another host, and an app served from `localhost` cannot be
pointed at a hosted environment (its session would be another origin's cookie). Run the app by running the Api, which
serves it.

Settings are read from `wwwroot/appsettings.json`, and from `wwwroot/appsettings.{environment}.json` when there is one
(`appsettings.Development.json` is). The environment is the one baked into the build: .NET 10 no longer reads the
`Blazor-Environment` response header, so it is the `WasmApplicationEnvironmentName` MSBuild property (Development for a
Debug build, Production for a Release publish unless it is set). `?environment=Development|Staging|Production` in the
address overrides it locally.

## First load and the Console (#645)

A visitor downloads the app to read a startlist, so its size is measured on what is delivered: `UiFirstLoadBudgetTests`
publishes the Ui for Release and sums the Brotli files that the Api serves (a debug build is several times larger and says
nothing about it). When it was written down the app was **8.29 MB over 180 files** (6.8 MB of assemblies, 0.9 MB of runtime,
0.3 MB of globalization data, 0.2 MB of scripts and styles), and the budget is a **ceiling of 9 MB**. A change that raises
the size past it raises it on purpose, and raises the ceiling in the test, here and in the ticket.

- `BlazorWebAssemblyLoadAllGlobalizationData` is on. A person who chooses Bulgarian or Turkish changes the culture of the app
  when it starts, which Blazor refuses with the globalization data that comes with the runtime (it has English and a few
  other languages); the full data costs about 0.2 MB more than that.
- What the app carries that a viewer does not need, and the first things to take out: the MongoDB driver (0.8 MB), OData (1.1
  MB: Core, Edm, the Api's and the model builder), AngleSharp and the Razor language (printing, 0.55 MB) and XML (0.36 MB).
  They arrive through `Not.Storage` and `Not.Application`, which the app uses for the repositories of its REST calls, so taking
  them out is a split of those projects and a task of its own.
- The pages of the Console are not part of it. `Features/Lazy/LazyRoutes` says which paths belong to an assembly that is fetched
  when a person goes there, and the router fetches it before it looks for a page (`WitnessBlazorRoot`). Nothing is lazy today.
  The Console (#646) fills the seam: it adds `["console"] = ["NoTiming.Console.wasm"]` to `LazyRoutes.Assemblies`, and the
  file as a `<BlazorWebAssemblyLazyLoad Include="NoTiming.Console.wasm" />` item in `NoTiming.Ui.csproj`; no code of the router
  changes.
