using NC.Application.RPC;
using Xunit;

namespace NC.Tests.RPC;

public sealed class RpcSettingsTests
{
    [Fact]
    public void Url_WhenEndpointPrefixIsSet_ShouldPlaceItBeforeHubPattern()
    {
        var settings = new RpcSettings
        {
            Host = "https://warp.example",
            EndpointPrefix = "event-bus",
            HubPattern = "witness",
        };

        Assert.Equal("https://warp.example/event-bus/witness", settings.Url);
    }

    [Fact]
    public void Url_WhenEndpointPrefixIsEmpty_ShouldUseHubPatternOnly()
    {
        var settings = new RpcSettings { Host = "https://warp.example", HubPattern = "witness" };

        Assert.Equal("https://warp.example/witness", settings.Url);
    }

    [Fact]
    public void Url_WhenEndpointPrefixHasSurroundingSlashes_ShouldNormalizeThem()
    {
        var settings = new RpcSettings
        {
            Host = "https://warp.example",
            EndpointPrefix = "/event-bus/",
            HubPattern = "witness",
        };

        Assert.Equal("https://warp.example/event-bus/witness", settings.Url);
    }

    [Fact]
    public void Url_WhenPortIsSet_ShouldIncludePortBeforeEndpointPrefix()
    {
        var settings = new RpcSettings
        {
            Host = "http://localhost",
            Port = 11337,
            EndpointPrefix = "event-bus",
            HubPattern = "witness",
        };

        Assert.Equal("http://localhost:11337/event-bus/witness", settings.Url);
    }
}
