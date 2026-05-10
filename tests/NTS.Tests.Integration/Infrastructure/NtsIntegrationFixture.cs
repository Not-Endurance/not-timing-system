using Testcontainers.MongoDb;

namespace NTS.Tests.Integration.Infrastructure;

public sealed class NtsIntegrationFixture : IAsyncLifetime
{
    MongoDbContainer? _mongo;
    NexusHttpProcess? _nexusHttp;
    WarpProcess? _warp;

    public Uri NexusBaseUrl => _nexusHttp?.BaseUrl ?? throw new InvalidOperationException("Nexus HTTP is not started.");
    public Uri WarpBaseUrl => _warp?.BaseUrl ?? throw new InvalidOperationException("Warp is not started.");

    public async Task InitializeAsync()
    {
        var paths = RepositoryPaths.Discover();
        _mongo = new MongoDbBuilder().WithImage("mongo:6.0").Build();
        await _mongo.StartAsync();

        _nexusHttp = new NexusHttpProcess(paths, PortAllocator.GetFreeTcpPort(), _mongo.GetConnectionString());
        await _nexusHttp.Start();

        _warp = new WarpProcess(paths, PortAllocator.GetFreeTcpPort(), _mongo.GetConnectionString());
        await _warp.Start();
    }

    public async Task DisposeAsync()
    {
        if (_warp != null)
        {
            await _warp.DisposeAsync();
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
