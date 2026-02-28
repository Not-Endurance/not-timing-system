Microsoft Entra ID settings are now present in `appsettings.json`, `appsettings.Development.json` and
`appsettings.Staging.json` (including `ClientSecret`) with placeholder values.

Recommended temporary local flow: replace those placeholders directly in appsettings.

Optional user-secrets override flow:

1. Run `dotnet user-secrets set "Authentication:DefaultChallengeScheme" "OpenIdConnect" --project src/Apps/Witness/NTS.Witness.Web`
2. Run `dotnet user-secrets set "Authentication:MicrosoftEntra:Instance" "https://login.microsoftonline.com" --project src/Apps/Witness/NTS.Witness.Web`
3. Run `dotnet user-secrets set "Authentication:MicrosoftEntra:TenantId" "YOUR_TENANT_ID" --project src/Apps/Witness/NTS.Witness.Web`
4. Run `dotnet user-secrets set "Authentication:MicrosoftEntra:ClientId" "YOUR_CLIENT_ID" --project src/Apps/Witness/NTS.Witness.Web`
5. Run `dotnet user-secrets set "Authentication:MicrosoftEntra:ClientSecret" "YOUR_CLIENT_SECRET" --project src/Apps/Witness/NTS.Witness.Web`
6. Run `dotnet user-secrets set "Authentication:MicrosoftEntra:CallbackPath" "/signin-oidc" --project src/Apps/Witness/NTS.Witness.Web`
7. Run `dotnet user-secrets set "Authentication:MicrosoftEntra:SignedOutCallbackPath" "/signout-callback-oidc" --project src/Apps/Witness/NTS.Witness.Web`

For Release pipeline use:

Authentication__DefaultChallengeScheme = OpenIdConnect
Authentication__MicrosoftEntra__Instance = ${{ secrets.ENTRA_AUTH_INSTANCE }}
Authentication__MicrosoftEntra__TenantId = ${{ secrets.ENTRA_AUTH_TENANT_ID }}
Authentication__MicrosoftEntra__ClientId = ${{ secrets.ENTRA_AUTH_CLIENT_ID }}
Authentication__MicrosoftEntra__ClientSecret = ${{ secrets.ENTRA_AUTH_CLIENT_SECRET }}
Authentication__MicrosoftEntra__CallbackPath = /signin-oidc
Authentication__MicrosoftEntra__SignedOutCallbackPath = /signout-callback-oidc
