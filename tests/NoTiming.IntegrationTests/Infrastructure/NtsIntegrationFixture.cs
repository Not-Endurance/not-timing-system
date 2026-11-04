using Testcontainers.MongoDb;

namespace NoTiming.IntegrationTests.Infrastructure;

public sealed class NtsIntegrationFixture : IAsyncLifetime
{
    MongoDbContainer? _mongo;
    NoTimingProcess? _warp;

    public Uri NoTimingBaseUrl => _warp?.BaseUrl ?? throw new InvalidOperationException("NoTiming is not started.");

    public async Task InitializeAsync()
    {
        var paths = RepositoryPaths.Discover();
        _mongo = new MongoDbBuilder().WithImage("mongo:6.0").Build();
        await _mongo.StartAsync();

        _warp = new NoTimingProcess(paths, PortAllocator.GetFreeTcpPort(), _mongo.GetConnectionString());
        await _warp.Start();
    }

    public async Task DisposeAsync()
    {
        if (_warp != null)
        {
            await _warp.DisposeAsync();
        }

        if (_mongo != null)
        {
            await _mongo.DisposeAsync();
        }
    }
}
