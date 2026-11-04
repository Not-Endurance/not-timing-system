using NC.Injection.Config;

namespace NC.Logging.Builder;

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
