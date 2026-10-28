using Testcontainers.MongoDb;

namespace NTS.Tests.Integration.Harness;

/// <summary>
/// MongoDB in a container and the Api in this process on a real loopback port, with a clock that a scenario can move.
/// It needs Docker and nothing else.
/// </summary>
public sealed class NtsIntegrationFixture : IAsyncLifetime
{
    MongoDbContainer? _mongo;
    ApiFactory? _api;

    /// <summary>The Api, for a test that signs a person in at it.</summary>
    internal ApiFactory Api => _api ?? throw new InvalidOperationException("The Api is not started.");

    /// <summary>The services of the Api, for a test that stands in for what the Api does after a write.</summary>
    internal IServiceProvider ApiServices => Api.Services;

    /// <summary>The clock of the Api, which a scenario moves forward to let an Event end (ADR-0007).</summary>
    public OffsetTimeProvider Clock { get; } = new();

    public string MongoConnectionString =>
        _mongo?.GetConnectionString() ?? throw new InvalidOperationException("MongoDB is not started.");
    public Uri ApiBaseUrl => _api?.BaseAddress ?? throw new InvalidOperationException("The Api is not started.");

    public async Task InitializeAsync()
    {
        _mongo = new MongoDbBuilder().WithImage("mongo:6.0").Build();
        await _mongo.StartAsync();

        _api = new ApiFactory(_mongo.GetConnectionString(), kestrel: true, time: Clock);
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
