using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;
using MongoDB.Bson;
using MongoDB.Driver;
using NTS.Tests.Integration.Infrastructure;
using NTS.Tools.IdentityMigration;

namespace NTS.Tests.Integration;

/// <summary>
/// ADR-0010, #636: <c>migrate-identities</c> as a dry run. It reads the Athletes and the Horses wherever the data of before keeps
/// them, on a MongoDB in a container, and writes a report and a decisions file and nothing else. The data holds everything the
/// real data does: the id 0 on several people, a person in both scripts bridged by a Setup row, a Setup duplicate, a copy whose
/// Setup row is gone, a FEI ID shared by two spellings, a FEI ID conflict, Bulgaria as BGR and BUL, near-miss Horse names and a
/// Participation counted in two Rankings.
/// </summary>
public sealed class IdentitiesMigrationTests : IClassFixture<MongoFixture>, IDisposable
{
    const string STEFAN = "athlete (fei 100): stefan kolev <> athlete (fei 101): stefan kolev";
    const string NIKOLA = "athlete (fei 400): nikola todorov <> athlete: nikola todorow";
    const string THUNDER = "horse: thunderbold <> horse: thunderbolt";

    readonly MongoFixture _mongo;
    readonly string _folder = Path.Combine(AppContext.BaseDirectory, "identities-" + Guid.NewGuid().ToString("N"));

    public IdentitiesMigrationTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    string ReportPath => Path.Combine(_folder, "report.md");
    string DecisionsPath => Path.Combine(_folder, "decisions.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_dry_run_changes_nothing_and_reports_the_counts_every_merge_by_its_rule_and_the_review_list()
    {
        var (name, database) = await SeededAsync();
        var before = await IdentityRegistryData.SnapshotAsync(database);

        var run = await RunAsync(name);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.Equal(before, await IdentityRegistryData.SnapshotAsync(database));
        var report = await File.ReadAllTextAsync(ReportPath);
        Assert.Contains("| Copies read | 22 | 17 |", report);
        Assert.Contains("| Copies in Setup rows | 7 | 2 |", report);
        Assert.Contains("| Copies in configure events | 2 | 2 |", report);
        Assert.Contains("| Copies in Participations | 9 | 9 |", report);
        Assert.Contains("| Copies in Rankings | 3 | 3 |", report); // a Participation in two Rankings: its copy is in both
        Assert.Contains("| Copies in Handouts | 1 | 1 |", report);
        Assert.Contains("| Distinct legacy ids (0 is none) | 8 | 9 |", report);
        Assert.Contains("| Identities found | 9 | 6 |", report);
        Assert.Contains("| Setup rows that exist | 7 | 2 |", report);
        Assert.Contains("| Rows that would be created | 3 | 4 |", report);
        Assert.Contains("| Setup duplicates that would merge | 1 | 0 |", report);
        Assert.Contains("| Copies whose Setup row is gone | 6 | 10 |", report);
        Assert.Contains("| Left to review | 2 | 1 |", report);
    }

    [Fact]
    public async Task Every_automatic_merge_is_listed_under_the_rule_that_made_it()
    {
        var (name, _) = await SeededAsync();

        await RunAsync(name);

        var report = await File.ReadAllTextAsync(ReportPath);
        var feiId = Between(report, "### Same FEI ID (", "### Same name (");
        Assert.Contains(
            "Athlete: \"Nikola Todorov\" (FEI ID 400, legacy id 6, Setup row) = \"Nikola Todorov\" (FEI ID 400, legacy id 6, 1 copy)",
            feiId
        ); // BG/BUL in Setup and BGR in the ride: the country decides nothing
        Assert.Contains(
            "Athlete: \"Iordan Ivanov\" (FEI ID 300, 1 copy) = \"Yordan Ivanov\" (FEI ID 300, 1 copy)",
            feiId
        );
        var exact = Between(report, "### Same name (", "### Same name in another script (");
        Assert.Contains(
            "Athlete: \"Maria Georgieva\" (legacy id 4, Setup row) = \"Maria Georgieva\" (legacy id 5, Setup row)",
            exact
        );
        Assert.Contains("Horse: \"Burya\" (legacy id 20, Setup row) = \"Burya\" (legacy id 20, 3 copies)", exact);
        var script = Between(report, "### Same name in another script (1)", "### Same legacy id (");
        Assert.Contains("\"Иван Петров\" (3 copies)", script);
        Assert.Contains("\"IVAN PETROV\" (2 copies)", script);
        Assert.Contains(
            "Athlete: \"Иван Петров / Ivan Petrov\" (legacy id 1, Setup row) = \"IVAN PETROV\" (2 copies)",
            exact
        ); // he is in both scripts, and so he bridges the two copies
    }

    [Fact]
    public async Task The_Setup_duplicates_the_rows_to_create_and_the_people_with_no_id_are_told()
    {
        var (name, _) = await SeededAsync();

        await RunAsync(name);

        var report = await File.ReadAllTextAsync(ReportPath);
        var duplicates = Between(report, "## Setup duplicates that would merge (1)", "## Rows that would be created (");
        Assert.Contains(
            "- Athlete: \"Maria Georgieva\" (legacy id 4, Setup row) + \"Maria Georgieva\" (legacy id 5, Setup row)\n",
            duplicates
        ); // the two rows, and not the copy of the same person that has no id
        var created = Between(report, "## Rows that would be created (7)", "## To review (");
        Assert.Contains("Athlete: \"Petar Dimitrov\" (legacy id 9, 3 copies)", created); // whose Setup row is gone
        Assert.Contains("Athlete: \"Todor Ivanov\" (1 copy)", created); // the id 0 joined nobody
        Assert.Contains("Horse: \"Nightfall\" (1 copy)", created);
        Assert.Contains(
            "Athlete: \"Iordan Ivanov\" (FEI ID 300, 1 copy) = \"Yordan Ivanov\" (FEI ID 300, 1 copy)",
            created
        ); // one FEI ID, two spellings, one row
        Assert.Contains(
            "Horse: \"Zora\" (legacy id 22, 3 copies) = \"Zora\" (legacy id 24, 1 copy) = \"Zora\" (legacy id 31, 1 copy)",
            created
        ); // the Horse is in a ride, a Handout, a configure event, and no Setup row
    }

    [Fact]
    public async Task The_review_list_has_the_conflict_the_spelling_variant_and_the_near_miss_with_their_evidence()
    {
        var (name, _) = await SeededAsync();

        await RunAsync(name);

        var report = await File.ReadAllTextAsync(ReportPath);
        Assert.Contains("## To review (3)", report);
        var conflict = Between(report, "### Conflicts (1)", "### Spelling variants (1)");
        Assert.Contains($"#### `{STEFAN}`", conflict);
        Assert.Contains("\"Stefan Kolev\" (FEI ID 100, legacy id 7, Setup row)", conflict);
        Assert.Contains("\"Stefan Kolev\" (FEI ID 101, legacy id 8, Setup row)", conflict);
        Assert.Contains("Events: 2026-12-04 Winter ride", conflict); // a Setup that has no Event yet is named by its first Competition
        Assert.Contains("Copies: configure events 1", conflict);
        var variant = Between(report, "### Spelling variants (1)", "### Near misses (1)");
        Assert.Contains($"#### `{NIKOLA}`", variant);
        Assert.Contains("Country: Bulgaria (BG/BUL)", variant);
        Assert.Contains("Country: Bulgaria (BGR)", variant);
        Assert.Contains("Events: 2026-04-26 to 2026-04-28 Spring ride", variant);
        Assert.Contains("Entered with: Zora", variant);
        Assert.Contains("\"Nikola Todorow\" (legacy id 10, Setup row)", variant);
        var nearMiss = report[report.IndexOf("### Near misses (1)", StringComparison.Ordinal)..];
        Assert.Contains($"#### `{THUNDER}`", nearMiss);
        Assert.Contains("\"Thunderbold\" (legacy id 23, 1 copy)", nearMiss);
        Assert.Contains("\"Thunderbolt\" (legacy id 30, Setup row)", nearMiss);
        Assert.Contains("  - Copies: Setup 1\n", nearMiss);
        Assert.Contains(": near miss\n", nearMiss);
    }

    [Fact]
    public async Task Running_it_twice_gives_the_same_report_and_the_decisions_file_the_first_run_wrote()
    {
        var (name, _) = await SeededAsync();

        var first = await RunAsync(name);
        var report = await File.ReadAllTextAsync(ReportPath);
        var decisions = await File.ReadAllTextAsync(DecisionsPath);
        var second = await RunAsync(name);

        Assert.Equal(report, await File.ReadAllTextAsync(ReportPath));
        Assert.Equal(decisions, await File.ReadAllTextAsync(DecisionsPath));
        Assert.Contains("(written)", first.Output);
        Assert.Contains("(unchanged)", second.Output);
        var written = JsonNode.Parse(decisions)!;
        Assert.Equal([STEFAN, NIKOLA, THUNDER], written["items"]!.AsObject().Select(x => x.Key)); // in order
        Assert.All(written["items"]!.AsObject(), x => Assert.Null(x.Value)); // nothing is decided yet
        Assert.Equal(
            ["Held", "Conflict", "SpellingVariant", "NearMiss"],
            written["rules"]!.AsObject().Select(x => x.Key)
        );
    }

    [Fact]
    public async Task Deciding_items_in_the_file_takes_them_out_of_the_review_and_keeps_the_rest_of_what_was_written()
    {
        var (name, _) = await SeededAsync();
        await RunAsync(name);

        await DecideAsync(items: new() { [STEFAN] = "different" });
        var afterOne = await RunAsync(name);
        var reportOne = await File.ReadAllTextAsync(ReportPath);
        await DecideAsync(items: new() { [NIKOLA] = "same" });
        await RunAsync(name);
        var reportTwo = await File.ReadAllTextAsync(ReportPath);

        Assert.Contains("| Left to review | 1 | 1 |", reportOne);
        Assert.DoesNotContain("### Conflicts", reportOne);
        Assert.Contains($"#### `{NIKOLA}`", reportOne);
        Assert.Contains("| Left to review | 0 | 1 |", reportTwo);
        Assert.DoesNotContain($"#### `{NIKOLA}`", reportTwo);
        Assert.Contains("| Identities found | 8 | 6 |", reportTwo); // Nikola Todorow is Nikola Todorov now
        Assert.Contains(
            $"`{NIKOLA}` (decided: same)",
            Between(reportTwo, "## Merges by your decisions (1)", "## Decisions")
        );
        Assert.Contains("Decisions that match nothing: 0", afterOne.Output);
        var kept = JsonNode.Parse(await File.ReadAllTextAsync(DecisionsPath))!["items"]!.AsObject();
        Assert.Equal("different", (string?)kept[STEFAN]); // what the owner wrote is still there
        Assert.Equal("same", (string?)kept[NIKOLA]);
        Assert.Null(kept[THUNDER]);
    }

    [Fact]
    public async Task A_rule_settles_every_item_of_a_class_and_an_item_of_its_own_beats_it()
    {
        var (name, _) = await SeededAsync();
        await RunAsync(name);

        await DecideAsync(
            rules: new()
            {
                ["Conflict"] = "different",
                ["SpellingVariant"] = "same",
                ["NearMiss"] = "different",
            },
            items: new() { [NIKOLA] = "different" }
        );
        await RunAsync(name);

        var report = await File.ReadAllTextAsync(ReportPath);
        Assert.Contains("Nothing is left to review.", report);
        Assert.Contains("| Identities found | 9 | 6 |", report); // the item beats the rule: the variant stays two
        Assert.Contains("- Decided by item: 1, by rule: 2", report);
    }

    [Fact]
    public async Task A_decision_that_names_nothing_in_the_data_is_reported_and_kept()
    {
        var (name, _) = await SeededAsync();
        await RunAsync(name);

        await DecideAsync(items: new() { ["athlete: nobody <> athlete: nowhere"] = "same" });
        var run = await RunAsync(name);

        Assert.Contains("Decisions that match nothing: 1", run.Output);
        Assert.Contains("`athlete: nobody <> athlete: nowhere`", await File.ReadAllTextAsync(ReportPath));
        Assert.True(
            JsonNode.Parse(await File.ReadAllTextAsync(DecisionsPath))!["items"]!
                .AsObject()
                .ContainsKey("athlete: nobody <> athlete: nowhere")
        );
    }

    [Fact]
    public async Task A_decisions_file_that_cannot_be_read_stops_the_run_and_is_left_as_it_was()
    {
        var (name, _) = await SeededAsync();
        Directory.CreateDirectory(_folder);
        await File.WriteAllTextAsync(DecisionsPath, """{ "items": { "a": "maybe" } }""");

        var run = await RunAsync(name);

        Assert.Equal(1, run.Code);
        Assert.Contains("Nothing was written.", run.Error);
        Assert.False(File.Exists(ReportPath));
        Assert.Equal("""{ "items": { "a": "maybe" } }""", await File.ReadAllTextAsync(DecisionsPath));
    }

    [Fact]
    public async Task The_pages_a_read_is_cut_into_do_not_change_what_is_found()
    {
        var (_, database) = await SeededAsync();

        var whole = await IdentitiesMigration.Plan(database, IdentityDecisions.None);
        var single = await IdentitiesMigration.Plan(database, IdentityDecisions.None, pageSize: 1);
        var three = await IdentitiesMigration.Plan(database, IdentityDecisions.None, pageSize: 3);

        var expected = IdentityReport.Render(whole, "nts");
        Assert.Equal(expected, IdentityReport.Render(single, "nts"));
        Assert.Equal(expected, IdentityReport.Render(three, "nts"));
        Assert.Equal(22, single.Athletes.Copies.Count);
    }

    [Fact]
    public async Task Without_a_database_name_it_reads_the_database_called_nts()
    {
        var database = new MongoClient(_mongo.ConnectionString).GetDatabase("nts");
        await database
            .GetCollection<BsonDocument>("athletes")
            .InsertOneAsync(new BsonDocument { ["_id"] = 1, ["Name"] = "Ivan Petrov" });

        var run = await RunAsync(null);

        Assert.Equal(0, run.Code);
        var report = await File.ReadAllTextAsync(ReportPath);
        Assert.Contains("Database: nts", report);
        Assert.Contains("| Copies read | 1 | 0 |", report);
    }

    [Fact]
    public async Task A_database_with_nothing_in_it_reports_nothing()
    {
        var name = "identities-" + Guid.NewGuid().ToString("N");

        var run = await RunAsync(name);

        Assert.Equal(0, run.Code);
        var report = await File.ReadAllTextAsync(ReportPath);
        Assert.Contains("| Copies read | 0 | 0 |", report);
        Assert.Contains("| Identities found | 0 | 0 |", report);
        Assert.Contains("Nothing is left to review.", report);
    }

    [Fact]
    public async Task The_report_and_the_decisions_go_by_default_to_a_folder_that_git_ignores()
    {
        var root = RepositoryPaths.Discover().Root;
        if (!Directory.Exists(Path.Combine(root, ".git")) && !File.Exists(Path.Combine(root, ".git")))
        {
            return; // a tree that is no clone (a source archive) has no git to ask; a worktree has a file called .git
        }

        foreach (
            var path in new[] { IdentitiesMigrationTool.DEFAULT_REPORT, IdentitiesMigrationTool.DEFAULT_DECISIONS }
        )
        {
            var start = new ProcessStartInfo("git", ["check-ignore", "-q", "--", path])
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardError = true,
            };
            Process process;
            try
            {
                process = Process.Start(start)!;
            }
            catch (Win32Exception)
            {
                return; // git is not here to ask; the paths are below .tmp, which .gitignore holds
            }

            using (process)
            {
                await process.WaitForExitAsync();
                Assert.True(
                    process.ExitCode == 0,
                    $"git does not ignore {path}: it would be committed with real names in it."
                );
            }
        }
    }

    [Fact]
    public async Task It_answers_to_its_name_as_a_command_of_its_own_and_never_prints_the_connection_string()
    {
        var (name, _) = await SeededAsync();
        Directory.CreateDirectory(_folder);

        var run = await RunProcessAsync(
            "migrate-identities",
            "--connection-string",
            _mongo.ConnectionString,
            "--database",
            name,
            "--report",
            ReportPath,
            "--decisions",
            DecisionsPath
        );
        var refused = await RunProcessAsync(
            "migrate-identities",
            "--connection-string",
            _mongo.ConnectionString,
            "--rollback" // an option it does not have: --apply is one since #637
        );

        Assert.Equal(0, run.Code);
        Assert.Contains("Dry-run identity migration.", run.Output);
        Assert.DoesNotContain(_mongo.ConnectionString, run.Output);
        Assert.True(File.Exists(ReportPath));
        Assert.Equal(1, refused.Code);
        Assert.DoesNotContain(_mongo.ConnectionString, refused.Output + refused.Error);
    }

    static string Between(string text, string from, string to)
    {
        var start = text.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"The report has no \"{from}\".");
        var end = text.IndexOf(to, start + from.Length, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    /// <summary>The command where its own build put it, in the configuration this test was built in (the tests are .NET 10, the command .NET 8).</summary>
    static string ToolPath()
    {
        var tests = new DirectoryInfo(AppContext.BaseDirectory); // <root>/tests/NTS.Tests.Integration/bin/<configuration>/net10.0
        var configuration = tests.Parent!.Name;
        var root = tests.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var path = Path.Combine(root, "tools", "NTS.Tools", "bin", configuration, "net8.0", "NTS.Tools.dll");
        return File.Exists(path) ? path : throw new FileNotFoundException("The tools were not built.", path);
    }

    static async Task<(int Code, string Output, string Error)> RunProcessAsync(params string[] arguments)
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

    async Task<(string Name, IMongoDatabase Database)> SeededAsync()
    {
        var name = "identities-" + Guid.NewGuid().ToString("N");
        var database = new MongoClient(_mongo.ConnectionString).GetDatabase(name);
        await IdentityRegistryData.SeedAsync(database);
        return (name, database);
    }

    /// <summary>The command, run in this process against the database, with its two files in the folder of this test.</summary>
    async Task<(int Code, string Output, string Error)> RunAsync(string? database)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        string[] named = database == null ? [] : ["--database", database];
        var code = await IdentitiesMigrationTool.Run(
            [
                "--connection-string",
                _mongo.ConnectionString,
                .. named,
                "--report",
                ReportPath,
                "--decisions",
                DecisionsPath,
            ],
            output,
            error
        );
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>What the owner does between two runs: edits the decisions file, leaving the rest of it as it is.</summary>
    async Task DecideAsync(Dictionary<string, string>? rules = null, Dictionary<string, string>? items = null)
    {
        var file = JsonNode.Parse(await File.ReadAllTextAsync(DecisionsPath))!.AsObject();
        foreach (var (key, value) in rules ?? [])
        {
            file["rules"]![key] = value;
        }

        foreach (var (key, value) in items ?? [])
        {
            file["items"]![key] = value;
        }

        await File.WriteAllTextAsync(DecisionsPath, file.ToJsonString(new() { WriteIndented = true }));
    }
}
