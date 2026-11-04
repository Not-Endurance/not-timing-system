using Testcontainers.MongoDb;

namespace NoTiming.IntegrationTests.Infrastructure;

public sealed class NtsIntegrationFixture : IAsyncLifetime
{
    MongoDbContainer? _mongo;
    NoTimingProcess? _noTiming;

    public Uri NoTimingBaseUrl => _noTiming?.BaseUrl ?? throw new InvalidOperationException("NoTiming is not started.");

    public async Task InitializeAsync()
    {
        var paths = RepositoryPaths.Discover();
        _mongo = new MongoDbBuilder().WithImage("mongo:6.0").Build();
        await _mongo.StartAsync();

        _noTiming = new NoTimingProcess(paths, PortAllocator.GetFreeTcpPort(), _mongo.GetConnectionString());
        await _noTiming.Start();
    }

    public async Task DisposeAsync()
    {
        if (_noTiming != null)
        {
            await _noTiming.DisposeAsync();
        }

        if (_mongo != null)
        {
            await _mongo.DisposeAsync();
        }
    }
}
