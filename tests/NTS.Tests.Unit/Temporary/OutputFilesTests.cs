using NTS.Tools.IdentityMigration;

namespace NTS.Tests.Unit.Temporary;

/// <summary>#636: the two files the dry run writes, and the decisions file that the owner edits and the run must not write over.</summary>
public sealed class OutputFilesTests : IDisposable
{
    readonly string _folder = Path.Combine(AppContext.BaseDirectory, "output-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_file_is_written_with_the_folders_on_the_way_in_UTF8_without_a_byte_order_mark()
    {
        var path = Path.Combine(_folder, "a", "b", "report.md");

        await OutputFiles.Write(path, "Иван Петров");

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal("Иван Петров", System.Text.Encoding.UTF8.GetString(bytes));
        Assert.NotEqual(0xEF, bytes[0]); // a byte order mark is the three bytes EF BB BF
    }

    [Fact]
    public async Task A_file_that_is_there_is_written_over()
    {
        var path = Path.Combine(_folder, "report.md");
        await OutputFiles.Write(path, "the first");

        await OutputFiles.Write(path, "the second");

        Assert.Equal("the second", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Decisions_that_are_the_file_as_it_was_are_not_written()
    {
        var path = Path.Combine(_folder, "decisions.json");
        await OutputFiles.Write(path, "as it was");
        var before = File.GetLastWriteTimeUtc(path);

        var saved = await OutputFiles.SaveDecisions(path, "as it was", "as it was");

        Assert.Equal(DecisionsSaved.Unchanged, saved);
        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public async Task Decisions_with_more_in_them_are_written_over_the_file_as_it_was_read()
    {
        var path = Path.Combine(_folder, "decisions.json");
        await OutputFiles.Write(path, "as it was");

        var saved = await OutputFiles.SaveDecisions(path, "as it was", "as it was and more");

        Assert.Equal(DecisionsSaved.Written, saved);
        Assert.Equal("as it was and more", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Decisions_are_written_when_there_was_no_file_and_there_is_none_yet()
    {
        var path = Path.Combine(_folder, "decisions.json");

        var saved = await OutputFiles.SaveDecisions(path, null, "the template");

        Assert.Equal(DecisionsSaved.Written, saved);
        Assert.Equal("the template", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task What_the_owner_saved_while_the_run_read_the_database_is_kept()
    {
        var path = Path.Combine(_folder, "decisions.json");
        await OutputFiles.Write(path, "what the run read");
        await OutputFiles.Write(path, "what the owner saved since"); // the owner decides an item while the run reads

        var saved = await OutputFiles.SaveDecisions(path, "what the run read", "what the run would write");

        Assert.Equal(DecisionsSaved.Kept, saved);
        Assert.Equal("what the owner saved since", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task A_file_that_appeared_while_the_run_read_the_database_is_kept()
    {
        var path = Path.Combine(_folder, "decisions.json");
        await OutputFiles.Write(path, "made by the owner meanwhile");

        var saved = await OutputFiles.SaveDecisions(path, null, "the template");

        Assert.Equal(DecisionsSaved.Kept, saved);
        Assert.Equal("made by the owner meanwhile", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task A_file_that_was_removed_while_the_run_read_the_database_is_not_made_again()
    {
        var path = Path.Combine(_folder, "decisions.json");

        var saved = await OutputFiles.SaveDecisions(path, "what the run read", "what the run would write");

        Assert.Equal(DecisionsSaved.Kept, saved);
        Assert.False(File.Exists(path));
    }
}
