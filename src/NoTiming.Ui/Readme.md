## Authentication - Microsoft Entra ID (WASM)
Configuration keys:
- `NClientAuthenticationSettings` for Witness sign-in and protected server access tokens

`NClientAuthenticationSettings` uses `ResourceClientId` for the target protected API app registration.
`NClientAuthenticationSettings.SessionLifetime` controls how long the device can reuse a successful login; it defaults to 24 hours.

`NoTiming.Ui` is a client-side Blazor WebAssembly app. It does not use confidential client secrets.
The app uses the normal Entra sign-in entrypoint for External ID sign-up/sign-in. Entra decides whether the user signs in or is taken through sign-up for the configured user flow.

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
