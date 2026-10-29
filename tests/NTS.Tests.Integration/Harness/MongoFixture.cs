namespace NTS.Tests.Integration.Harness;

/// <summary>A database of the class in the run's MongoDB, for tests that bring their own host or none at all.</summary>
public sealed class MongoFixture : IAsyncLifetime
{
    MongoLease? _mongo;

    public string ConnectionString =>
        _mongo?.ConnectionString ?? throw new InvalidOperationException("MongoDB is not started.");

    public async Task InitializeAsync()
    {
        _mongo = await MongoLease.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_mongo != null)
        {
            await _mongo.DisposeAsync();
        }
    }
}
