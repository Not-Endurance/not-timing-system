using Testcontainers.MongoDb;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// MongoDB in a container and the legacy Functions API as a child process, until the Functions API is retired (#647),
/// next to the Api in this process on a real loopback port.
/// </summary>
public sealed class NtsIntegrationFixture : IAsyncLifetime
{
    MongoDbContainer? _mongo;
    FunctionsHttpProcess? _functionsHttp;
    ApiFactory? _api;

    /// <summary>The services of the Api, for a test that stands in for what the Api does after a write.</summary>
    internal IServiceProvider ApiServices =>
        _api?.Services ?? throw new InvalidOperationException("The Api is not started.");

    public string MongoConnectionString =>
        _mongo?.GetConnectionString() ?? throw new InvalidOperationException("MongoDB is not started.");
    public Uri FunctionsBaseUrl =>
        _functionsHttp?.BaseUrl ?? throw new InvalidOperationException("The Functions API is not started.");
    public Uri ApiBaseUrl => _api?.BaseAddress ?? throw new InvalidOperationException("The Api is not started.");

    public async Task InitializeAsync()
    {
        var paths = RepositoryPaths.Discover();
        _mongo = new MongoDbBuilder().WithImage("mongo:6.0").Build();
        await _mongo.StartAsync();

        _functionsHttp = await PortAllocator.StartOnAFreePort(async port =>
        {
            var host = new FunctionsHttpProcess(paths, port, _mongo.GetConnectionString());
            try
            {
                await host.Start();
                return host;
            }
            catch
            {
                await host.DisposeAsync();
                throw;
            }
        });

        _api = new ApiFactory(_mongo.GetConnectionString(), kestrel: true);
        _ = _api.BaseAddress; // starts the host
    }

    public async Task DisposeAsync()
    {
        if (_api != null)
        {
            await _api.DisposeAsync();
        }

        if (_functionsHttp != null)
        {
            await _functionsHttp.DisposeAsync();
        }

        if (_mongo != null)
        {
            await _mongo.DisposeAsync();
        }
    }
}
