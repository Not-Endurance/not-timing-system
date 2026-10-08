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
