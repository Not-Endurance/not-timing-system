## Authentication - Microsoft Entra ID
Configuration - Authentication:MicrosoftEntra

Note: `ClientSecret` is not committed for obvious reasons. Locally it's hooked with dotnet UserSecrets

### Localhost
Run `dotnet user-secrets --id nts-localhost set "Authentication:MicrosoftEntra:ClientSecret" "<value>"`

### Staging 
Run `dotnet user-secrets --id nts-staging set "Authentication:MicrosoftEntra:ClientSecret" "<value>"`

### Production
Run `dotnet user-secrets --id nts-production set "Authentication:MicrosoftEntra:ClientSecret" "<value>"`
