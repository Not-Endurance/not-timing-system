using Not.Injection.Config;

namespace Not.Logging.Builder;

public class HttpLoggerContext : INConfig
{
    public string Host { get; set; } = default!;

    void INConfig.Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new ApplicationException(
                $"'{nameof(HttpLoggerContext)}.{nameof(Host)}' cannot be null or whitespace"
            );
        }
    }
}
