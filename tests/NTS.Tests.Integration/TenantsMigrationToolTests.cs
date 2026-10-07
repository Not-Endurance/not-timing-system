using MongoDB.Bson;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Tests.Integration.Infrastructure;
using NTS.Tools.Tenants;

namespace NTS.Tests.Integration;

/// <summary>
/// The command line of <c>migrate-tenants</c> (#607): a dry run unless it is given <c>--apply</c>, which needs the environment
/// the database is, and what it prints names neither the connection string nor anything an account keeps.
/// </summary>
public sealed class TenantsMigrationToolTests : IClassFixture<MongoFixture>
{
    static readonly DateTimeOffset NOW = new(2031, 3, 14, 12, 0, 0, TimeSpan.Zero);

    readonly MongoFixture _mongo;

    public TenantsMigrationToolTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task Without_apply_it_reports_and_changes_nothing()
    {
        var data = await LegacyAsync();
        var before = await data.AllAsync();

        var (code, output, error) = await RunAsync(data, "--environment", "Staging");

        Assert.Equal(0, code);
        Assert.Contains("Dry-run tenants migration.", output);
        Assert.Contains($"Database: {data.Database.DatabaseNamespace.DatabaseName}", output);
        Assert.Contains("Nothing was changed. Run again with --apply to persist.", output);
        Assert.Equal("", error);
        Assert.Equal(before, await data.AllAsync());
    }

    [Fact]
    public async Task With_apply_and_the_environment_it_migrates_marks_the_database_and_says_so()
    {
        var data = await LegacyAsync();
        await data.AccountAsync("ivan@example.test", "Bulgaria");

        var (code, output, _) = await RunAsync(data, "--apply", "--environment", "staging");

        Assert.Equal(0, code);
        Assert.Contains("Applying tenants migration.", output);
        Assert.Contains("Applied.", output);
        Assert.Equal("Staging", await EnvironmentMarker.ReadAsync(data.Database));
        Assert.Equal("country-bg", (await data.ManyAsync("users")).Single()["HomeTenantId"].AsString);
    }

    [Fact]
    public async Task With_apply_and_no_environment_it_refuses_with_a_status_that_tells_so_and_writes_nothing()
    {
        var data = await LegacyAsync();
        var before = await data.AllAsync();

        var (code, output, _) = await RunAsync(data, "--apply");

        Assert.Equal(1, code);
        Assert.Contains("Refused:", output);
        Assert.Contains("--environment", output);
        Assert.Contains("Nothing was changed: refused.", output);
        Assert.Equal(before, await data.AllAsync());
    }

    [Fact]
    public async Task The_account_named_with_main_operator_is_the_one_that_is_given_the_Events()
    {
        var data = await LegacyAsync();
        var account = await data.AccountAsync("main@example.test", "Bulgaria");
        var setup = await data.SetupAsync();

        var (code, _, _) = await RunAsync(
            data,
            "--apply",
            "--environment",
            "Staging",
            "--main-operator",
            "Main@Example.Test"
        );

        Assert.Equal(0, code);
        Assert.Equal(account, (await data.OneAsync("configure_events", setup))["MainOperatorId"].AsGuid);
    }

    [Fact]
    public async Task It_prints_neither_the_connection_string_nor_what_an_account_keeps()
    {
        var data = await LegacyAsync();
        await data.AccountAsync("secret.person@example.test", "Bulgaria", x => x["Name"] = "Secret Person");

        var (_, output, error) = await RunAsync(data, "--apply", "--environment", "Staging");

        foreach (var text in new[] { output, error })
        {
            Assert.DoesNotContain(_mongo.ConnectionString, text);
            Assert.DoesNotContain("secret.person", text);
            Assert.DoesNotContain("Secret Person", text);
        }
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--environment")] // without its value
    public async Task An_option_it_does_not_know_or_that_lacks_its_value_prints_the_usage_and_fails(string option)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await TenantsMigrationTool.Run(
            ["--connection-string", _mongo.ConnectionString, option],
            output,
            error,
            NOW
        );

        Assert.Equal(1, code);
        Assert.Contains("Unknown or incomplete option", error.ToString());
        Assert.Contains("Usage:", output.ToString());
    }

    [Fact]
    public async Task Without_a_connection_string_it_says_so_and_shows_the_usage()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await TenantsMigrationTool.Run([], output, error, NOW);

        Assert.Equal(1, code);
        Assert.Contains("--connection-string is required.", error.ToString());
        Assert.Contains("migrate-tenants", output.ToString());
    }

    [Fact]
    public async Task Help_describes_the_options_and_succeeds()
    {
        var output = new StringWriter();

        var code = await TenantsMigrationTool.Run(["--help"], output, new StringWriter(), NOW);

        Assert.Equal(0, code);
        var text = output.ToString();
        foreach (
            var option in new[] { "--connection-string", "--database", "--environment", "--main-operator", "--apply" }
        )
        {
            Assert.Contains(option, text);
        }
    }

    async Task<(int Code, string Output, string Error)> RunAsync(LegacyTenantData data, params string[] more)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var args = new List<string>
        {
            "--connection-string",
            _mongo.ConnectionString,
            "--database",
            data.Database.DatabaseNamespace.DatabaseName,
        };
        args.AddRange(more);
        var code = await TenantsMigrationTool.Run([.. args], output, error, NOW);
        return (code, output.ToString(), error.ToString());
    }

    async Task<LegacyTenantData> LegacyAsync()
    {
        var data = new LegacyTenantData(
            new MongoClient(_mongo.ConnectionString).GetDatabase("tenants-" + Guid.NewGuid().ToString("N"))
        );
        await data.BulgariaAsync();
        return data;
    }
}
