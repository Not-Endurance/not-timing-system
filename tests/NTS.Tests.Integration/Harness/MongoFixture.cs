namespace NTS.Tests.Integration.Harness;

/// <summary>A MongoDB in a container, for tests that bring their own host or none at all.</summary>
public sealed class MongoFixture : IAsyncLifetime
{
    MongoContainer? _mongo;

    public string ConnectionString =>
        _mongo?.ConnectionString ?? throw new InvalidOperationException("MongoDB is not started.");

    public async Task InitializeAsync()
    {
        _mongo = await MongoContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_mongo != null)
        {
            await _mongo.DisposeAsync();
        }
    }
}
