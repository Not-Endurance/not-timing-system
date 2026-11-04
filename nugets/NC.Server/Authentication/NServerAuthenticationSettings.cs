namespace Not.Server.Authentication;

public class NServerAuthenticationSettings
{
    public string? AuthorityInstance { get; set; }
    public string? AuthorityTenantId { get; set; }
    public string? SignInClientId { get; set; }
    public string? SignInClientSecret { get; set; }
    public string? ResourceClientId { get; set; }
    public string? Audience { get; set; }
    public string? Scope { get; set; }
    public string[]? AccessTokenQueryPaths { get; set; }
}
