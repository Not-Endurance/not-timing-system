using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The host of a test is the same on every machine (#607). The local workflow keeps what a developer runs the platform with
/// (the allow-list of the local sign in as, a connection string) in their user-secrets, which a host in Development reads,
/// so the host of a test is built without them: a test that expects no allow-list must not fail on the machine of a developer
/// who has one.
/// </summary>
public sealed class TestHostIsolationTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public TestHostIsolationTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task A_test_host_in_Development_reads_no_user_secrets_of_the_machine_it_runs_on()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString, environment: "Development");

        var configuration = (IConfigurationRoot)api.Services.GetRequiredService<IConfiguration>();

        Assert.DoesNotContain(
            configuration.Providers.OfType<JsonConfigurationProvider>(),
            x => x.Source.Path == ApiFactory.USER_SECRETS_FILE
        );
    }
}
