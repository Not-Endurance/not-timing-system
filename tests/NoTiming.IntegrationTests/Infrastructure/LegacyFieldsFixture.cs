using NoTiming.IntegrationTests.Authentication.Harness;
using Testcontainers.MongoDb;

namespace NoTiming.IntegrationTests.Infrastructure;

/// <summary>
/// Starts Mongo, writes a row with the legacy top-level profile fields, and only then starts the host, so the host's startup
/// migration is the step that moves those fields into the profile.
/// </summary>
public sealed class LegacyFieldsFixture : IAsyncLifetime
{
    public const string Email = "legacy.fields@example.test";
    public const int UserId = 900101;

    MongoDbContainer? _mongo;
    NoTimingProcess? _noTiming;

    public Uri NoTimingBaseUrl => _noTiming?.BaseUrl ?? throw new InvalidOperationException("NoTiming is not started.");

    public int NoTimingPort => _noTiming?.Port ?? throw new InvalidOperationException("NoTiming is not started.");

    public string MongoConnectionString =>
        _mongo?.GetConnectionString() ?? throw new InvalidOperationException("Mongo is not started.");

    public async Task InitializeAsync()
    {
        var paths = RepositoryPaths.Discover();
        _mongo = new MongoDbBuilder().WithImage("mongo:6.0").Build();
        await _mongo.StartAsync();

        await new MongoAccess(MongoConnectionString).SeedLegacyFieldsUserAsync(
            UserId,
            Email,
            "Petra",
            "Legacy",
            "BG",
            "Konarche",
            "20202020"
        );

        _noTiming = new NoTimingProcess(paths, PortAllocator.GetFreeTcpPort(), MongoConnectionString);
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
