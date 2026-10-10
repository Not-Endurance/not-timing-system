using NoTiming.Ui.Features.Lazy;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The pages of the Console are not part of what a viewer downloads to open the app (#645): the app knows which paths belong
/// to an assembly that is fetched when a person goes there, and the Console (#646) names its own. A path that is not under
/// such a prefix needs nothing, and one that is needs the files of its prefix, whatever the case and the slashes.
/// </summary>
public sealed class LazyRoutesTests
{
    static readonly IReadOnlyDictionary<string, string[]> TABLE = new Dictionary<string, string[]>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["console"] = ["NoTiming.Console.wasm", "NoTiming.Console.Setup.wasm"],
        ["reports"] = ["NoTiming.Reports.wasm"],
    };

    [Theory]
    [InlineData("console")]
    [InlineData("/console")]
    [InlineData("console/")]
    [InlineData("Console/events/3f2504e0-4f89-41d3-9a0c-0305e82c3301/setup")]
    [InlineData("/CONSOLE/athletes?sort=name")]
    [InlineData("console#top")]
    [InlineData("console?tab=1")]
    [InlineData("/console/?tab=1#top")]
    public void A_path_under_a_prefix_needs_the_files_of_the_prefix(string path)
    {
        Assert.Equal(["NoTiming.Console.wasm", "NoTiming.Console.Setup.wasm"], LazyRoutes.NeededBy(path, TABLE));
    }

    [Fact]
    public void Each_prefix_needs_its_own_files_and_no_other()
    {
        Assert.Equal(["NoTiming.Reports.wasm"], LazyRoutes.NeededBy("/reports/2030", TABLE));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("startlist")]
    [InlineData("events/3f2504e0-4f89-41d3-9a0c-0305e82c3301/snapshot")]
    [InlineData("historic-events")]
    [InlineData("profile")]
    [InlineData("consoles")]
    [InlineData("my-console/events")]
    [InlineData("events/console")]
    public void A_path_that_is_not_under_a_prefix_needs_nothing_and_a_prefix_is_a_whole_segment(string path)
    {
        Assert.Empty(LazyRoutes.NeededBy(path, TABLE));
    }

    [Fact]
    public void Nothing_is_fetched_when_no_prefix_names_a_file()
    {
        Assert.Empty(LazyRoutes.NeededBy("console/setup", new Dictionary<string, string[]>()));
    }
}
