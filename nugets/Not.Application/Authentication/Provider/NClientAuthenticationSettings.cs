namespace Not.Application.Authentication.Provider;

public class NClientAuthenticationSettings
{
    public string? Instance { get; set; }
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? ResourceClientId { get; set; }
    public string? Audience { get; set; }
    public string? Scope { get; set; }

    // Device-only reuse window for a successful login. Defaults to 24 hours.
    public TimeSpan? SessionLifetime { get; set; } = TimeSpan.FromHours(24);
}
