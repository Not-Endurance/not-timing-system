using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration.Architecture;

/// <summary>
/// The layering of the repository as the project files say it (ADR-0011, #647): the one web app is the Api, which serves the Ui, and the
/// Functions API, Warp and the libraries that served them are gone. The rules are tested on their own in <see cref="ArchitectureRulesTests"/>.
/// </summary>
public sealed class RepositoryArchitectureTests
{
    static readonly ProjectGraph GRAPH = ProjectGraph.Load(RepositoryPaths.Discover().Root);

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

    static void Holds(IReadOnlyList<string> violations)
    {
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
