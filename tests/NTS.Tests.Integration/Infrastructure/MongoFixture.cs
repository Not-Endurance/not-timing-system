using Testcontainers.MongoDb;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>A MongoDB in a container, for tests that bring their own host or none at all.</summary>
public sealed class MongoFixture : IAsyncLifetime
{
    MongoDbContainer? _mongo;

    public string ConnectionString =>
        _mongo?.GetConnectionString() ?? throw new InvalidOperationException("MongoDB is not started.");

    public async Task InitializeAsync()
    {
        _mongo = new MongoDbBuilder().WithImage("mongo:6.0").Build();
        await _mongo.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_mongo != null)
        {
            await _mongo.DisposeAsync();
        }
    }
}
