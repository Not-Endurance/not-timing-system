## Authentication - sign-in by code, over a session cookie
The sign-in and registration pages are `/authentication` and `/register`. A one-time code is sent by email, and the host sets an HttpOnly session cookie when it is entered correctly.

The browser keeps no token and no sign-in state in storage. The signed-in state comes from `api/me/profile`, which answers 401 when there is no session. Anonymous visitors use the public pages as before.

Passkey sign-in and enrolment are served by the host; their browser page is not built yet.

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
Blazor's configured application environment. Deployed builds are served by NoTiming, which sends the
`Blazor-Environment` header from its own `ASPNETCORE_ENVIRONMENT`.

Deployed builds do not set `RpcSettings.Host` or `NHttpSettings.Url`. They call the origin the UI is served from, which is
the NoTiming instance. `localhostsettings.*.json` and `appsettings.Development.json` point at a NoTiming instance for localhost runs.
