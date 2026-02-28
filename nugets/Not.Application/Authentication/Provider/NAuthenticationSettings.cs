using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Not.Application.Authentication.Provider;

public class NAuthenticationSettings
{
    public const string Scheme = OpenIdConnectDefaults.AuthenticationScheme;
    public string? Instance { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? CallbackPath { get; set; }
    public string? SignedOutCallbackPath { get; set; }
}
