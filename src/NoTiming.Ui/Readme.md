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

Environment configuration files are loaded from:
- `wwwroot/appsettings.json`
- `wwwroot/appsettings.Development.json`
- `wwwroot/appsettings.Staging.json`
- `wwwroot/appsettings.Production.json`

Localhost-only overrides are loaded when running from localhost:
- `localhostsettings.Staging.json`
- `localhostsettings.Production.json`

The `Development`, `Staging`, and `Production` launch profiles pass `?environment=...` so localhost can choose
the target environment file while still running from `localhost`. If no environment is supplied, the client uses
the environment baked into the build: .NET 10 no longer reads the `Blazor-Environment` response header, so the
environment is the `WasmApplicationEnvironmentName` MSBuild property (Development for a Debug build, Production for
a Release publish unless it is set). The delivery workflows publish with
`-p:WasmApplicationEnvironmentName=Staging` or `Production`. A build without it that is deployed to staging would
silently load `appsettings.Production.json` and connect to the production hub.
