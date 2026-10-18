using NTS.Tools.IdentityMigration;

namespace NTS.Tests.Unit.Temporary;

/// <summary>#636: the command line of <c>migrate-identities</c>, as far as it can be driven without a database.</summary>
public sealed class IdentitiesMigrationToolTests : IDisposable
{
    readonly string _folder = Path.Combine(AppContext.BaseDirectory, "identities-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task Help_says_what_the_options_are_and_where_the_files_go_by_default()
    {
        var (code, output, error) = await Run("--help");

        Assert.Equal(0, code);
        Assert.Equal("", error);
        Assert.Contains("migrate-identities --connection-string <mongo>", output);
        Assert.Contains("--report <path>", output);
        Assert.Contains("--decisions <path>", output);
        Assert.Contains(IdentitiesMigrationTool.DEFAULT_REPORT, output);
        Assert.Contains(IdentitiesMigrationTool.DEFAULT_DECISIONS, output);
        Assert.DoesNotContain("--apply", output); // this command only reports
    }

    [Fact]
    public async Task The_files_default_to_a_folder_that_git_ignores()
    {
        Assert.StartsWith(".tmp/", IdentitiesMigrationTool.DEFAULT_REPORT, StringComparison.Ordinal);
        Assert.StartsWith(".tmp/", IdentitiesMigrationTool.DEFAULT_DECISIONS, StringComparison.Ordinal);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Without_a_connection_string_it_asks_for_one_and_exits_with_1()
    {
        var (code, output, error) = await Run();

        Assert.Equal(1, code);
        Assert.Contains("--connection-string is required.", error);
        Assert.Contains("Usage:", output);
    }

    [Theory]
    [InlineData("--apply")]
    [InlineData("--bogus")]
    public async Task An_option_it_does_not_have_is_refused_and_this_command_cannot_be_told_to_apply(string option)
    {
        var (code, _, error) = await Run("--connection-string", "mongodb://never-used", option);

        Assert.Equal(1, code);
        Assert.Contains($"Unknown or incomplete option '{option}'.", error);
    }

    [Fact]
    public async Task An_option_that_lacks_its_value_is_refused()
    {
        var (code, _, error) = await Run("--connection-string", "mongodb://never-used", "--report");

        Assert.Equal(1, code);
        Assert.Contains("Unknown or incomplete option '--report'.", error);
    }

    [Fact]
    public async Task The_report_and_the_decisions_cannot_be_the_same_file()
    {
        var same = Path.Combine(_folder, "both.txt");

        var (code, _, error) = await Run(
            "--connection-string",
            "mongodb://never-used",
            "--report",
            same,
            "--decisions",
            same
        );

        Assert.Equal(1, code);
        Assert.Contains("--report and --decisions must be two files.", error);
        Assert.False(File.Exists(same));
    }

    [Fact]
    public async Task A_decisions_file_that_cannot_be_read_stops_the_run_before_anything_is_read_or_written()
    {
        Directory.CreateDirectory(_folder);
        var decisions = Path.Combine(_folder, "decisions.json");
        var report = Path.Combine(_folder, "report.md");
        await File.WriteAllTextAsync(decisions, """{ "items": { "athlete: a <> athlete: b": "yes" } }""");

        var (code, _, error) = await Run(
            "--connection-string",
            "mongodb://never-used",
            "--report",
            report,
            "--decisions",
            decisions
        );

        Assert.Equal(1, code);
        Assert.Contains(decisions, error);
        Assert.Contains("athlete: a <> athlete: b", error);
        Assert.Contains("Nothing was written.", error);
        Assert.False(File.Exists(report));
        Assert.Equal("""{ "items": { "athlete: a <> athlete: b": "yes" } }""", await File.ReadAllTextAsync(decisions));
    }

    static async Task<(int Code, string Output, string Error)> Run(params string[] arguments)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await IdentitiesMigrationTool.Run(arguments, output, error);
        return (code, output.ToString(), error.ToString());
    }
}
