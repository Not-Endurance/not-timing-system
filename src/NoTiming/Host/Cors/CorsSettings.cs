namespace NoTiming.Host.Cors;

public class CorsSettings
{
    public string[] AllowedOrigins { get; init; } = [];
    public string[] AllowedOriginPatterns { get; init; } = [];
}
