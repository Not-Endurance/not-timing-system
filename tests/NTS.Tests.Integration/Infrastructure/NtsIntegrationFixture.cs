using Testcontainers.MongoDb;

namespace NTS.Tests.Integration.Infrastructure;

public sealed class NtsIntegrationFixture : IAsyncLifetime
{
    MongoDbContainer? _mongo;
    NexusHttpProcess? _nexusHttp;
    ApiProcess? _api;

    public Uri NexusBaseUrl => _nexusHttp?.BaseUrl ?? throw new InvalidOperationException("Nexus HTTP is not started.");
    public Uri ApiBaseUrl => _api?.BaseUrl ?? throw new InvalidOperationException("The Api is not started.");

    public async Task InitializeAsync()
    {
        var paths = RepositoryPaths.Discover();
        _mongo = new MongoDbBuilder().WithImage("mongo:6.0").Build();
        await _mongo.StartAsync();

        _nexusHttp = new NexusHttpProcess(paths, PortAllocator.GetFreeTcpPort(), _mongo.GetConnectionString());
        await _nexusHttp.Start();

        _api = new ApiProcess(paths, PortAllocator.GetFreeTcpPort());
        await _api.Start();
    }

    public async Task DisposeAsync()
    {
        if (_api != null)
        {
            await _api.DisposeAsync();
        }

        if (_nexusHttp != null)
        {
            await _nexusHttp.DisposeAsync();
        }

        if (_mongo != null)
        {
            await _mongo.DisposeAsync();
        }
    }
}
