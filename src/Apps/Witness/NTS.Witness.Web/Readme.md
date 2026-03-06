## Authentication - Microsoft Entra ID (WASM)
Configuration key: `NAuthenticationSettings`

`NTS.Witness.Web` is now a client-side Blazor WebAssembly app. It does not use confidential client secrets.

Environment configuration files are loaded from:
- `wwwroot/appsettings.json`
- `wwwroot/appsettings.Development.json`
- `wwwroot/appsettings.Staging.json`
