namespace Not.Application.HTTP;

public class NHttpSettings
{
    const string HTTP = "http";
    const string HTTPS = "https";

    string? _customUrl;

    public string? Url
    {
        get => _customUrl ?? BuildUrl();
        set => _customUrl = value;
    }

    public bool UseHttps { get; set; } = true;
    public string? Host { get; set; }
    public string? Endpoint { get; set; }

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
