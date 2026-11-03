namespace NTS.Tests.Integration.Harness;

/// <summary>
/// The Api of a test class, in memory, on a database of the class in the run's MongoDB. The tests of a class run one at a time,
/// so they share the host and its database, as they shared the database before: each brings its own people, Events and
/// codes. A test that needs a host of its own, with a clock, settings or services of its own, builds one with
/// <c>new ApiFactory</c>.
/// </summary>
public sealed class SharedApiFixture : IAsyncLifetime
{
    MongoLease? _mongo;
    ApiFactory? _api;

    /// <summary>The Api of the class. It starts with the first request a test makes to it.</summary>
    internal ApiFactory Api => _api ?? throw new InvalidOperationException("The Api is not started.");

    public string MongoConnectionString =>
        _mongo?.ConnectionString ?? throw new InvalidOperationException("MongoDB is not started.");

    public async Task InitializeAsync()
    {
        _mongo = await MongoLease.StartAsync();
        _api = new ApiFactory(_mongo.ConnectionString);
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
