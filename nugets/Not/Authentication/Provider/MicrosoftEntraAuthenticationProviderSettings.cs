using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Not.Authentication.Provider;

public class MicrosoftEntraAuthenticationProviderSettings
{
    public const string SECTION_NAME = $"{NAuthenticationProviderSettings.SECTION_NAME}:MicrosoftEntra";
    public string Scheme { get; set; } = OpenIdConnectDefaults.AuthenticationScheme;
    public string? Authority { get; set; }
    public string Instance { get; set; } = "https://login.microsoftonline.com";
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";
}
