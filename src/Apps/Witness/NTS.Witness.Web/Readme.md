## Authentication - Microsoft Entra ID (WASM)
Configuration keys:
- `NClientAuthenticationSettings` for Witness sign-in and protected server access tokens

`NClientAuthenticationSettings` uses `ResourceClientId` for the target protected API app registration.
`NClientAuthenticationSettings.SessionLifetime` controls how long the device can reuse a successful login; it defaults to 24 hours.

`NTS.Witness.Web` is now a client-side Blazor WebAssembly app. It does not use confidential client secrets.
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
Blazor's configured application environment. Azure Static Web Apps deployments set this by writing the
`Blazor-Environment` header to `staticwebapp.config.json` during the workflow run.
