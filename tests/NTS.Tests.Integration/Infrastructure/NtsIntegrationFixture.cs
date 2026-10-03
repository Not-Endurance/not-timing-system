using Testcontainers.MongoDb;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// MongoDB in a container and the legacy Functions API as a child process, until the Functions API is retired (#647),
/// next to the Api in this process on a real loopback port.
/// </summary>
public sealed class NtsIntegrationFixture : IAsyncLifetime
{
    MongoDbContainer? _mongo;
    NexusHttpProcess? _nexusHttp;
    ApiFactory? _api;

    public string MongoConnectionString =>
        _mongo?.GetConnectionString() ?? throw new InvalidOperationException("MongoDB is not started.");
    public Uri NexusBaseUrl => _nexusHttp?.BaseUrl ?? throw new InvalidOperationException("Nexus HTTP is not started.");
    public Uri ApiBaseUrl => _api?.BaseAddress ?? throw new InvalidOperationException("The Api is not started.");

    public async Task InitializeAsync()
    {
        var paths = RepositoryPaths.Discover();
        _mongo = new MongoDbBuilder().WithImage("mongo:6.0").Build();
        await _mongo.StartAsync();

        _nexusHttp = new NexusHttpProcess(paths, PortAllocator.GetFreeTcpPort(), _mongo.GetConnectionString());
        await _nexusHttp.Start();

        _api = new ApiFactory(kestrel: true);
        _ = _api.BaseAddress; // starts the host
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
