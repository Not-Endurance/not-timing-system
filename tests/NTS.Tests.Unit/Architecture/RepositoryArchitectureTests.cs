using NTS.Tests.Unit.Harness.Architecture;

namespace NTS.Tests.Unit.Architecture;

/// <summary>
/// The layering of the repository as the project files say it (ADR-0011, #647): the one web app is the Api, which serves the Ui, and the
/// Functions API, Warp and the libraries that served them are gone. The rules are tested on their own in <see cref="ArchitectureRulesTests"/>.
/// </summary>
public sealed class RepositoryArchitectureTests
{
    static readonly string ROOT = RepositoryRoot.Find(AppContext.BaseDirectory, "AGENTS.md", "src");
    static readonly ProjectGraph GRAPH = ProjectGraph.Load(ROOT, "src", "nugets", "tests", "tools");

    [Fact]
    public void The_graph_is_the_one_of_the_product()
    {
        // one of each folder the rules read, so that a rule cannot hold for the want of a project to look at
        foreach (
            var name in new[]
            {
                "NoTiming.Api",
                "NoTiming.Ui",
                "NoTiming.Console",
                "NTS.Domain",
                "NTS.Contracts",
                "Not",
                "NTS.Tools",
                "NTS.Tests.Integration",
            }
        )
        {
            Assert.True(GRAPH.Has(name), $"The project files hold no {name}.");
        }
    }

    [Fact]
    public void The_Ui_never_references_the_Api()
    {
        Holds(ArchitectureRules.UiNeverReferencesApi(GRAPH));
    }

    [Fact]
    public void The_domain_references_no_infrastructure()
    {
        Holds(ArchitectureRules.DomainReferencesNoInfrastructure(GRAPH));
    }

    [Fact]
    public void Nothing_references_Azure_Functions()
    {
        Holds(ArchitectureRules.NothingReferencesAzureFunctions(GRAPH));
    }

    [Fact]
    public void Every_project_is_on_net10()
    {
        Holds(ArchitectureRules.EveryProjectIsOnNet10(GRAPH));
    }

    [Fact]
    public void The_projects_that_retired_with_the_Functions_API_stay_gone()
    {
        Holds(ArchitectureRules.RetiredProjectsStayGone(GRAPH));
    }

    [Fact]
    public void Only_the_tests_take_Playwright()
    {
        Holds(ArchitectureRules.OnlyTestsTakePlaywright(GRAPH));
    }

    [Fact]
    public void Only_the_endpoint_filter_and_the_places_that_tell_ask_the_policy()
    {
        Holds(ArchitectureRules.OnlyTheFilterAndThePlacesThatTellAskThePolicy(SourcesOfTheApi()));
    }

    static List<(string Path, string Text)> SourcesOfTheApi()
    {
        return
        [
            .. Directory
                .EnumerateFiles(Path.Combine(ROOT, "src", "NoTiming.Api"), "*.cs", SearchOption.AllDirectories)
                .Select(x => (Path.GetRelativePath(ROOT, x).Replace('\\', '/'), File.ReadAllText(x)))
                .Where(x => !x.Item1.Contains("/obj/") && !x.Item1.Contains("/bin/")),
        ];
    }

    [Fact]
    public void The_unit_tests_mirror_the_product()
    {
        Holds(ArchitectureRules.TestsMirrorTheProduct(UnitTestFiles(), ProductFolders()));
    }

    [Fact]
    public void A_test_file_declares_tests_and_nothing_else()
    {
        Holds(
            ArchitectureRules.TestFilesDeclareOnlyTests(
                UnitTestFiles().Where(x => !x.Path.StartsWith("Harness/", StringComparison.Ordinal)).ToList()
            )
        );
    }

    [Fact]
    public void The_harness_holds_no_tests()
    {
        Holds(
            ArchitectureRules.HarnessHoldsNoTests(
                UnitTestFiles().Where(x => x.Path.StartsWith("Harness/", StringComparison.Ordinal)).ToList()
            )
        );
    }

    [Fact]
    public void The_shared_test_library_references_no_project_of_the_product()
    {
        Holds(ArchitectureRules.NotTestReferencesNoProduct(GRAPH));
    }

    [Fact]
    public void The_integration_tests_live_in_the_folders_of_the_features_of_the_product()
    {
        Holds(ArchitectureRules.IntegrationTestsLiveInFeatures(IntegrationTestFiles(), IntegrationFeatures()));
    }

    [Fact]
    public void The_integration_harness_holds_no_tests()
    {
        var harness = IntegrationTestFiles().Where(x => x.Path.StartsWith("Harness/", StringComparison.Ordinal)).ToList();

        Holds(ArchitectureRules.HarnessHoldsNoTests(harness));
    }

    [Fact]
    public void The_parked_scenarios_stay_out_of_the_build()
    {
        var project = Path.Combine(ROOT, "tests", "NTS.Tests.Integration", "NTS.Tests.Integration.csproj");

        Holds(ArchitectureRules.ParkedScenariosStayOutOfTheBuild(File.ReadAllText(project)));
    }

    static List<(string Path, string Text)> IntegrationTestFiles()
    {
        return FilesUnder(Path.Combine(ROOT, "tests", "NTS.Tests.Integration"));
    }

    static List<string> IntegrationFeatures()
    {
        return
        [
            .. new[] { "NoTiming.Api", "NoTiming.Ui" }
                .SelectMany(x => Directory.EnumerateDirectories(Path.Combine(ROOT, "src", x, "Features")))
                .Select(x => new DirectoryInfo(x).Name),
            "Platform",
            "Tools",
            "Ui",
        ];
    }

    static List<(string Path, string Text)> UnitTestFiles()
    {
        return FilesUnder(Path.Combine(ROOT, "tests", "NTS.Tests.Unit"));
    }

    static List<(string Path, string Text)> FilesUnder(string folder)
    {
        return
        [
            .. Directory
                .EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
                .Select(x => (Path.GetRelativePath(folder, x).Replace('\\', '/'), File.ReadAllText(x)))
                .Where(x => !x.Item1.StartsWith("obj/", StringComparison.Ordinal))
                .Where(x => !x.Item1.StartsWith("bin/", StringComparison.Ordinal)),
        ];
    }

    static List<string> ProductFolders()
    {
        return
        [
            .. new[] { "src", "nugets", "tools" }
                .SelectMany(x =>
                    Directory
                        .EnumerateDirectories(Path.Combine(ROOT, x), "*", SearchOption.AllDirectories)
                        .Select(folder => Path.GetRelativePath(Path.Combine(ROOT, x), folder).Replace('\\', '/'))
                )
                .Where(x => !x.Split('/').Any(part => part is "bin" or "obj" or "node_modules")),
        ];
    }

    static void Holds(IReadOnlyList<string> violations)
    {
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
