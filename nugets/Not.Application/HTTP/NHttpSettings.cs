using Not.Application.Configurations;

namespace Not.Application.HTTP;

public class NHttpSettings
{
    const string HTTP = "http";
    const string HTTPS = "https";

    string? _customUrl;

    public string? Url
    {
        get => _customUrl ?? BuildUrl();
        init => _customUrl = value;
    }

    public bool UseHttps { get; init; } = true;
    public string? Host { get; init; }
    public string? Endpoint { get; init; }

    string? BuildUrl()
    {
        if (Host == null)
        {
            return null;
        }
        var url = $"{(UseHttps ? HTTPS : HTTP)}://{HttpHelper.NormalizeUri(Host)}";
        if (Endpoint != null)
        {
            url = $"{url}/{HttpHelper.NormalizeUri(Endpoint)}";
        }
        return url;
    }
}
