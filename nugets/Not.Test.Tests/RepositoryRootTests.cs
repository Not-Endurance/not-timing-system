namespace Not.Test.Tests;

public sealed class RepositoryRootTests : IDisposable
{
    readonly string _folder = Path.Combine(AppContext.BaseDirectory, "root-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Find_climbs_to_the_first_folder_that_holds_every_marker()
    {
        var root = Path.Combine(_folder, "repo");
        var nested = Path.Combine(root, "src", "deep", "folder");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), string.Empty);

        Assert.Equal(root, RepositoryRoot.Find(nested, "AGENTS.md", "src"));
    }

    [Fact]
    public void Find_takes_a_folder_marker_as_well_as_a_file()
    {
        var root = Path.Combine(_folder, "repo");
        var nested = Path.Combine(root, "tests");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "AGENTS.md"), string.Empty);

        Assert.Equal(root, RepositoryRoot.Find(nested, "src", "AGENTS.md"));
    }

    [Fact]
    public void Find_throws_when_no_folder_above_holds_every_marker()
    {
        var marker = "marker-" + Guid.NewGuid().ToString("N");

        Assert.Throws<InvalidOperationException>(() => RepositoryRoot.Find(_folder, marker));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
