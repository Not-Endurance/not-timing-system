namespace Not.Authentication.Provider;

public class NAuthenticationProviderSettings
{
    public const string SECTION_NAME = "Authentication";
    public string? DefaultChallengeScheme { get; set; }
    public MicrosoftEntraAuthenticationProviderSettings MicrosoftEntra { get; set; } = new();
}
