To run NTS.Witness.Web in Debug mode User Secrets must be registered. The steps are:

1. Run `dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID" --project Witness\NTS.Witness.Web`
2. Run `dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_ACTUAL_SECRET" --project Witness\NTS.Witness.Web`

For Release pipeline use:

Authentication__Google__ClientId = ${{ secrets.GOOGLE_AUTH_CLIENT_ID }}
Authentication__Google__ClientSecret = ${{ secrets.GOOGLE_AUTH_CLIENT_SECRET }}