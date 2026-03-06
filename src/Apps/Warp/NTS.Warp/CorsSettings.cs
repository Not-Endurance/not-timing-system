namespace NTS.Warp;

public class CorsSettings
{
    public const string POLICY_NAME = "NtsWarpCors";

    public string[] AllowedOrigins { get; init; } = [];
    public string[] AllowedOriginHostPatterns { get; init; } = [];
}
