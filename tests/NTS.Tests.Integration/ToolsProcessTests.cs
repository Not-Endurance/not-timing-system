using System.Diagnostics;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Storage.Mongo;
using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration;

/// <summary>
/// The commands of <c>tools/NTS.Tools</c> as the owner runs them (#607): a process of their own, with the arguments on the
/// command line. That shows what only a fresh process shows: the name each command answers to, its exit code, and the
/// mapping of the models that the command sets up itself, which the tests that run a command inside this process find
/// already set up by the hosts they start.
/// </summary>
public sealed class ToolsProcessTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public ToolsProcessTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task The_seed_run_as_a_command_of_its_own_stores_the_models_the_way_the_Api_writes_them()
    {
        var name = NewDatabaseName();

        var run = await RunAsync(
            "seed-staging",
            "--connection-string",
            _mongo.ConnectionString,
            "--database",
            name,
            "--environment",
            "Staging",
            "--tenant-root",
            "root@example.test",
            "--main-operator",
            "main@example.test",
            "--official",
            "steward@example.test:Steward",
            "--apply"
        );

        Assert.Equal(0, run.Code);
        Assert.Contains("Applied.", run.Output);
        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(name);
        var setup = await database
            .GetCollection<BsonDocument>("configure_events")
            .Find(FilterDefinition<BsonDocument>.Empty)
            .SingleAsync();
        var competition = setup["Competitions"][0];
        Assert.Equal("FEI", competition["Ruleset"].AsString); // an enum is its name
        Assert.Equal(BsonType.DateTime, competition["Start"].BsonType); // an instant is a date
        Assert.Equal(BsonBinarySubType.UuidStandard, setup["_id"].AsBsonBinaryData.SubType); // an id is a standard UUID
        var user = await database
            .GetCollection<BsonDocument>("users")
            .Find(new BsonDocument("Email", "root@example.test"))
            .SingleAsync();
        Assert.Equal(BsonBinarySubType.UuidStandard, user["_id"].AsBsonBinaryData.SubType);
    }

    [Fact]
    public async Task Each_command_answers_to_its_name_and_says_in_its_exit_code_whether_it_went_through()
    {
        var name = NewDatabaseName();
        var data = new LegacyTenantData(new MongoClient(_mongo.ConnectionString).GetDatabase(name));
        await data.BulgariaAsync();
        await data.AccountAsync("ivan@example.test", "Bulgaria");

        var dry = await RunAsync("migrate-tenants", "--connection-string", _mongo.ConnectionString, "--database", name);
        var refused = await RunAsync(
            "migrate-tenants",
            "--connection-string",
            _mongo.ConnectionString,
            "--database",
            name,
            "--apply"
        );
        var marked = await RunAsync(
            "mark-environment",
            "--connection-string",
            _mongo.ConnectionString,
            "--database",
            name,
            "--environment",
            "Staging",
            "--apply"
        );
        var applied = await RunAsync(
            "migrate-tenants",
            "--connection-string",
            _mongo.ConnectionString,
            "--database",
            name,
            "--environment",
            "Staging",
            "--apply"
        );

        Assert.Equal(0, dry.Code);
        Assert.Contains("Dry-run tenants migration.", dry.Output);
        Assert.Equal(1, refused.Code); // an apply that does not say which environment the database is
        Assert.Contains("Nothing was changed: refused.", refused.Output);
        Assert.Equal(0, marked.Code);
        Assert.Equal("Staging", await EnvironmentMarker.ReadAsync(data.Database));
        Assert.Equal(0, applied.Code);
        Assert.Contains("Applied.", applied.Output);
        foreach (var text in new[] { dry.Output, refused.Output, marked.Output, applied.Output })
        {
            Assert.DoesNotContain(_mongo.ConnectionString, text);
        }
    }

    static string NewDatabaseName()
    {
        return "tools-" + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// The command where its own build put it, in the configuration this test was built in. The copy beside the tests is
    /// not the one to run: the tests are .NET 10 and their folder holds the libraries of that, which the command, a .NET 8
    /// program, cannot load.
    /// </summary>
    static string ToolPath()
    {
        var tests = new DirectoryInfo(AppContext.BaseDirectory); // <root>/tests/NTS.Tests.Integration/bin/<configuration>/net10.0
        var configuration = tests.Parent!.Name;
        var root = tests.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var path = Path.Combine(root, "tools", "NTS.Tools", "bin", configuration, "net8.0", "NTS.Tools.dll");
        return File.Exists(path) ? path : throw new FileNotFoundException("The tools were not built.", path);
    }

    static async Task<(int Code, string Output, string Error)> RunAsync(params string[] arguments)
    {
        var tool = ToolPath();
        var start = new ProcessStartInfo(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet"
        )
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(tool)!,
        };
        start.ArgumentList.Add(tool);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("The command did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return (process.ExitCode, await output, await error);
    }
}
