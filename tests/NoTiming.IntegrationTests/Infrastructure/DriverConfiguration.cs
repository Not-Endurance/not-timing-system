using Microsoft.Extensions.Configuration;
using NC.Application.HTTP;
using NC.Application.RPC;
using NoTiming.Application.Contracts;

namespace NoTiming.IntegrationTests.Infrastructure;

// Builds the configuration a driver's service provider needs to reach the test NoTiming host.
internal static class DriverConfiguration
{
    public static IConfiguration Create(
        Uri baseUrl,
        string hub,
        string clientName,
        IEnumerable<KeyValuePair<string, string?>>? additionalSettings = null
    )
    {
        var host = baseUrl.ToString().TrimEnd('/');
        var settings = new Dictionary<string, string?>
        {
            [$"{nameof(RpcSettings)}:{nameof(RpcSettings.Host)}"] = host,
            [$"{nameof(RpcSettings)}:{nameof(RpcSettings.HubPattern)}"] = hub,
            [$"{nameof(RpcSettings)}:{nameof(RpcSettings.EndpointPrefix)}"] = ApplicationConstants.EVENT_BUS,
            [$"{nameof(RpcSettings)}:{nameof(RpcSettings.ClientName)}"] = clientName,
            [$"{nameof(RpcSettings)}:{nameof(RpcSettings.AppVersion)}"] = "integration-test",
            [$"{nameof(RpcSettings)}:{nameof(RpcSettings.ConnectTimeoutSeconds)}"] = "10",
            [$"{nameof(NHttpSettings)}:{nameof(NHttpSettings.Host)}"] = host,
            [$"{nameof(NHttpSettings)}:{nameof(NHttpSettings.EndpointPrefix)}"] = "api",
        };

        if (additionalSettings != null)
        {
            foreach (var setting in additionalSettings)
            {
                settings[setting.Key] = setting.Value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
