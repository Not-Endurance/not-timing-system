To run NTS.Witness.Web User Secrets must be registered. The steps are:

1. Run `dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID" --project Witness\NTS.Witness.Web`
2. Run `dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_ACTUAL_SECRET" --project Witness\NTS.Witness.Web`