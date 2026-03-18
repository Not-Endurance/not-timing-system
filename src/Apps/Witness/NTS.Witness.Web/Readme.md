## Authentication - Microsoft Entra ID (WASM)
Configuration key: `NAuthenticationSettings`

`NTS.Witness.Web` is now a client-side Blazor WebAssembly app. It does not use confidential client secrets.
The app uses the normal Entra sign-in entrypoint for External ID sign-up/sign-in. Entra decides whether the user signs in or is taken through sign-up for the configured user flow.

Environment configuration files are loaded from:
- `wwwroot/appsettings.json`
- `wwwroot/appsettings.Development.json`
- `wwwroot/appsettings.Staging.json`

Localhost-only overrides are loaded when `IS_LOCALHOST=true`:
- `localhostsettings.Staging.json`
- `localhostsettings.Production.json`

The `Staging` and `Production` launch profiles pass `?environment=...` so localhost can choose
the target environment file while still running from `localhost`.
