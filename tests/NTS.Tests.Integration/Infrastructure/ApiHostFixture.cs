using Testcontainers.MongoDb;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The Api alone, on a real loopback port, with a MongoDB in a container for its users, sessions and codes. It needs
/// Docker and nothing else: no Functions API and no Azurite.
/// </summary>
public sealed class ApiHostFixture : IAsyncLifetime
{
    MongoDbContainer? _mongo;
    ApiFactory? _api;

    internal ApiFactory Api => _api ?? throw new InvalidOperationException("The Api is not started.");
    public string MongoConnectionString =>
        _mongo?.GetConnectionString() ?? throw new InvalidOperationException("MongoDB is not started.");
    public Uri BaseAddress => Api.BaseAddress;

    public async Task InitializeAsync()
    {
        _mongo = new MongoDbBuilder().WithImage("mongo:6.0").Build();
        await _mongo.StartAsync();

        _api = new ApiFactory(_mongo.GetConnectionString(), kestrel: true);
        _ = _api.BaseAddress; // starts the host
    }

    public async Task DisposeAsync()
    {
        if (_api != null)
        {
            await _api.DisposeAsync();
        }

        if (_mongo != null)
        {
            await _mongo.DisposeAsync();
        }
    }
}
