namespace Not.Application.HTTP;

public class NHttpSettings
{
    string? _customUrl;

    public string? Url
    {
        get => _customUrl ?? BuildUrl();
        set => _customUrl = value;
    }

    public bool UseHttps { get; set; } = true;
    public string? Host { get; set; }
    public string? EndpointPrefix { get; set; }

    string? BuildUrl()
    {
        if (Host == null)
        {
            return null;
        }
        var url = HttpHelper.NormalizeUri(Host);
        if (EndpointPrefix != null)
        {
            url = $"{url}/{HttpHelper.NormalizeUri(EndpointPrefix)}";
        }
        return url;
    }
}
