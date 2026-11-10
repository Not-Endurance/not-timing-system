using System.Collections.Concurrent;
using NoTiming.IntegrationTests.Authentication.Harness;
using Testcontainers.MongoDb;

namespace NoTiming.IntegrationTests.Infrastructure;

public sealed class NoTimingIntegrationFixture : IAsyncLifetime
{
    readonly ConcurrentDictionary<string, Task<string>> _sessions = new(StringComparer.OrdinalIgnoreCase);
    MongoDbContainer? _mongo;
    NoTimingProcess? _noTiming;

    public Uri NoTimingBaseUrl => _noTiming?.BaseUrl ?? throw new InvalidOperationException("NoTiming is not started.");

    public int NoTimingPort => _noTiming?.Port ?? throw new InvalidOperationException("NoTiming is not started.");

    public string MongoConnectionString =>
        _mongo?.GetConnectionString() ?? throw new InvalidOperationException("MongoDB is not started.");

    /// <summary>
    /// The session cookie of a person signed in to this host. The first call signs the person in through the account
    /// routes, reading the code from the outbox, or registers them when no account exists yet. Later calls reuse the
    /// cookie, which keeps the sign-in code limits out of the way of a scenario that opens several drivers.
    /// </summary>
    internal Task<string> SignInAsync(IntegrationUser user)
    {
        return _sessions.GetOrAdd(user.Email, _ => SignInCoreAsync(user));
    }

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

    async Task<string> SignInCoreAsync(IntegrationUser user)
    {
        using var account = new AccountClient(NoTimingBaseUrl, NoTimingPort);
        var mongo = new MongoAccess(MongoConnectionString);

        if (await mongo.UserExistsAsync(user.Email))
        {
            return await AccountTestData.SignInWithCodeAsync(account, mongo, user.Email);
        }

        return await AccountTestData.RegisterAndSignInAsync(
            account,
            mongo,
            user.Email,
            givenName: user.GivenName ?? "Integration",
            surname: user.Surname ?? "Witness",
            country: user.CountryIsoCode is { Length: 2 } code ? code : "BG"
        );
    }
}
