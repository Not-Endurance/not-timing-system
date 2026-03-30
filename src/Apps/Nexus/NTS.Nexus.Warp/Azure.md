Create App Registration
- 
Sign in to the Microsoft Entra admin center and switch to your External ID tenant.
1. Go to Entra ID > App registrations > New registration.
2. Name it something like NTS Warp API.
3. For Supported account types, choose Accounts in this organizational directory only.
4. Leave Redirect URI empty - This is an API, not an interactive app.
5. Click Register.

On the Overview page, copy these values:
Application (client) ID
Directory (tenant) ID

Expose the API
-
1. Open the new NTS Warp API registration.
2. Go to Expose an API.
3. Click Add next to Application ID URI.
4. Accept the default api://<warp-client-id> and save.
5. Click Add a scope. Use a shared delegated scope name such as nts-client-scope so other server applications can reuse the same client-side auth pattern.
6. Click State: Enabled
7. Save the scope.

The full scope will be:
api://<warp-client-id>/nts-client-scope

The audience Warp should validate will be:
api://<warp-client-id>

Optional but Recommended
-
1. Go to Owners.
2. Add yourself as owner.
3. Also make sure the Witness SPA app registration has an owner too.

This helps the Warp API show up under My APIs.

Authorize Witness To Call Warp
-
Do this on the Witness app registration, not the Warp one.

1. Open the Witness SPA app registration.
2. Go to API permissions > Add a permission > My APIs.
3. Select NTS Warp API.
4. Choose Delegated permissions.
5. Select nts-client-scope.
6. Click Add permissions.
7. click "Grant admin consent" in API permissions table headers.

You can also pre-authorize the Witness client from Warp’s Expose an API > Authorized client applications, but it’s optional.

Configuration Pattern
-
- Keep the interactive app registration in `NClientAuthenticationSettings`.
- Configure each protected server application in `NServerAuthenticationSettings` with:
  - `AuthorityInstance`: the Entra authority base URL for that server app
  - `AuthorityTenantId`: the tenant id used to validate issued tokens
  - `ResourceClientId`: the API app registration client id
  - `Scope`: `nts-client-scope`
  - `AccessTokenQueryPaths`: SignalR/API paths that accept bearer tokens from the query string
- The client requests `api://<server-app-client-id>/nts-client-scope`.
- The server validates `api://<server-app-client-id>` as audience and then applies its own business authorization.
