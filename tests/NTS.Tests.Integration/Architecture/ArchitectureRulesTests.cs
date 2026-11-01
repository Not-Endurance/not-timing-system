namespace NTS.Tests.Integration.Architecture;

/// <summary>
/// The rules of the layering (ADR-0011, #647) against project files made up for the purpose: each rule finds the project that breaks it,
/// says which, and leaves alone the layering that the decision asks for. <see cref="RepositoryArchitectureTests"/> asks them about the repository.
/// </summary>
public sealed class ArchitectureRulesTests
{
    [Fact]
    public void The_Api_references_the_Ui_that_it_serves()
    {
        var graph = ProjectGraph.Of(Project("NoTiming.Api", references: ["NoTiming.Ui"]), Project("NoTiming.Ui"));

        Assert.Empty(ArchitectureRules.UiNeverReferencesApi(graph));
    }

    [Fact]
    public void The_Ui_does_not_reference_the_Api()
    {
        var graph = ProjectGraph.Of(Project("NoTiming.Ui", references: ["NoTiming.Api"]), Project("NoTiming.Api"));

        var violation = Assert.Single(ArchitectureRules.UiNeverReferencesApi(graph));

        Assert.Equal("NoTiming.Ui references NoTiming.Api, which serves it and so must not be known to it.", violation);
    }

    [Fact]
    public void The_Ui_does_not_reach_the_Api_through_a_project_it_references()
    {
        var graph = ProjectGraph.Of(
            Project("NoTiming.Ui", references: ["NTS.Contracts"]),
            Project("NTS.Contracts", references: ["NoTiming.Api"]),
            Project("NoTiming.Api")
        );

        var violation = Assert.Single(ArchitectureRules.UiNeverReferencesApi(graph));

        Assert.Equal("NoTiming.Ui references NoTiming.Api, which serves it and so must not be known to it.", violation);
    }

    [Fact]
    public void A_rule_about_the_Ui_cannot_pass_in_a_repository_that_has_none()
    {
        var graph = ProjectGraph.Of(Project("NoTiming.Api"));

        Assert.Throws<InvalidOperationException>(() => ArchitectureRules.UiNeverReferencesApi(graph));
    }

    [Fact]
    public void The_domain_references_the_rest_of_the_domain_and_the_base_libraries()
    {
        var graph = ProjectGraph.Of(
            Project("Not", "nugets", packages: ["Serilog", "Microsoft.Extensions.Localization"]),
            Project("Not.Domain", "nugets", references: ["Not"], packages: ["MediatR.Contracts"]),
            Project("NTS", references: ["Not"]),
            Project("NTS.Domain", "src/Domain", references: ["Not.Domain", "NTS"]),
            Project("NTS.Domain.Core", "src/Domain", references: ["NTS.Domain"]),
            Project("NTS.Domain.Setup", "src/Domain", references: ["NTS.Domain"])
        );

        Assert.Empty(ArchitectureRules.DomainReferencesNoInfrastructure(graph));
    }

    [Fact]
    public void The_domain_does_not_reference_a_project_that_is_not_the_domain()
    {
        var graph = ProjectGraph.Of(
            Project("NTS.Domain.Core", "src/Domain", references: ["Not.Storage"]),
            Project("Not.Storage", "nugets")
        );

        var violation = Assert.Single(ArchitectureRules.DomainReferencesNoInfrastructure(graph));

        Assert.Equal("NTS.Domain.Core reaches Not.Storage, which is not part of the domain.", violation);
    }

    [Fact]
    public void The_domain_does_not_reach_a_project_that_is_not_the_domain_through_the_base_library()
    {
        var graph = ProjectGraph.Of(
            Project("NTS.Domain", "src/Domain", references: ["NTS"]),
            Project("NTS", references: ["Not.Storage"]),
            Project("Not.Storage", "nugets")
        );

        var violation = Assert.Single(ArchitectureRules.DomainReferencesNoInfrastructure(graph));

        Assert.Equal("NTS.Domain reaches Not.Storage, which is not part of the domain.", violation);
    }

    [Theory]
    [InlineData("MongoDB.Driver")]
    [InlineData("mongodb.driver")]
    [InlineData("MongoDB.Bson")]
    [InlineData("Microsoft.AspNetCore.OData")]
    [InlineData("Microsoft.Azure.Cosmos")]
    [InlineData("Azure.Monitor.OpenTelemetry.AspNetCore")]
    [InlineData("OpenTelemetry.Extensions.Hosting")]
    [InlineData("Microsoft.Playwright")]
    [InlineData("MudBlazor")]
    public void The_domain_does_not_take_a_package_of_infrastructure(string package)
    {
        var graph = ProjectGraph.Of(Project("Not.Domain", "nugets", packages: [package]));

        var violation = Assert.Single(ArchitectureRules.DomainReferencesNoInfrastructure(graph));

        Assert.Equal(
            $"Not.Domain reaches the package {package} in Not.Domain, which the domain must not know.",
            violation
        );
    }

    [Fact]
    public void The_domain_does_not_reach_a_package_of_infrastructure_through_the_base_library()
    {
        var graph = ProjectGraph.Of(
            Project("NTS.Domain", "src/Domain", references: ["Not"]),
            Project("Not", "nugets", packages: ["MongoDB.Driver"])
        );

        var violation = Assert.Single(ArchitectureRules.DomainReferencesNoInfrastructure(graph));

        Assert.Equal(
            "NTS.Domain reaches the package MongoDB.Driver in Not, which the domain must not know.",
            violation
        );
    }

    [Fact]
    public void A_rule_about_the_domain_cannot_pass_in_a_repository_that_has_none()
    {
        var graph = ProjectGraph.Of(Project("NoTiming.Api"));

        Assert.Throws<InvalidOperationException>(() => ArchitectureRules.DomainReferencesNoInfrastructure(graph));
    }

    [Theory]
    [InlineData("Microsoft.Azure.Functions.Worker")]
    [InlineData("microsoft.azure.functions.worker")]
    [InlineData("Microsoft.Azure.Functions.Worker.Sdk")]
    [InlineData("Microsoft.NET.Sdk.Functions")]
    [InlineData("Microsoft.Azure.WebJobs.Extensions.Storage")]
    public void Nothing_takes_a_package_of_Azure_Functions(string package)
    {
        var graph = ProjectGraph.Of(Project("NoTiming.Api"), Project("NTS.Tools", "tools", packages: [package]));

        var violation = Assert.Single(ArchitectureRules.NothingReferencesAzureFunctions(graph));

        Assert.Equal($"tools/NTS.Tools/NTS.Tools.csproj takes the package {package}.", violation);
    }

    [Fact]
    public void Nothing_is_an_Azure_Functions_app()
    {
        var graph = ProjectGraph.Of(Project("NoTiming.Api"), Project("NTS.Host", azureFunctions: true));

        var violation = Assert.Single(ArchitectureRules.NothingReferencesAzureFunctions(graph));

        Assert.Equal("src/NTS.Host/NTS.Host.csproj is an Azure Functions app.", violation);
    }

    [Fact]
    public void A_project_with_other_packages_is_not_taken_for_Azure_Functions()
    {
        var graph = ProjectGraph.Of(
            Project(
                "NoTiming.Api",
                packages:
                [
                    "Microsoft.AspNetCore.Components.WebAssembly.Server",
                    "Azure.Monitor.OpenTelemetry.AspNetCore",
                ]
            )
        );

        Assert.Empty(ArchitectureRules.NothingReferencesAzureFunctions(graph));
    }

    [Theory]
    [InlineData("net10.0")]
    [InlineData("net10.0-android")]
    public void A_project_on_net10_is_on_net10(string target)
    {
        var graph = ProjectGraph.Of(Project("NoTiming.Api", target: target));

        Assert.Empty(ArchitectureRules.EveryProjectIsOnNet10(graph));
    }

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("netstandard2.1")]
    [InlineData("net10.0;net8.0")]
    [InlineData("")]
    public void A_project_that_is_not_only_on_net10_is_named(string target)
    {
        var graph = ProjectGraph.Of(Project("NoTiming.Api"), Project("Not", "nugets", target: target));

        var violation = Assert.Single(ArchitectureRules.EveryProjectIsOnNet10(graph));

        Assert.Equal($"nugets/Not/Not.csproj targets '{target}' and not only net10.0.", violation);
    }

    [Theory]
    [InlineData("NTS.Application")]
    [InlineData("NTS.Storage")]
    [InlineData("NTS.Client")]
    [InlineData("Not.Server")]
    [InlineData("NTS.Nexus.HTTP")]
    public void A_project_that_retired_with_the_Functions_API_does_not_come_back(string name)
    {
        var graph = ProjectGraph.Of(Project("NoTiming.Api"), Project(name, "src/Apps"));

        var violation = Assert.Single(ArchitectureRules.RetiredProjectsStayGone(graph));

        Assert.Equal($"src/Apps/{name}/{name}.csproj is a project that retired with the Functions API.", violation);
    }

    [Fact]
    public void The_projects_that_replaced_them_are_not_taken_for_retired_ones()
    {
        var graph = ProjectGraph.Of(
            Project("NoTiming.Api"),
            Project("NoTiming.Ui"),
            Project("Not.Storage", "nugets"),
            Project("Not.Identity", "nugets")
        );

        Assert.Empty(ArchitectureRules.RetiredProjectsStayGone(graph));
    }

    [Theory]
    [InlineData("Microsoft.Playwright")]
    [InlineData("Microsoft.Playwright.Xunit")]
    [InlineData("microsoft.playwright")]
    public void A_project_of_the_product_does_not_take_Playwright(string package)
    {
        var graph = ProjectGraph.Of(Project("NoTiming.Api", packages: [package]));

        var violation = Assert.Single(ArchitectureRules.OnlyTestsTakePlaywright(graph));

        Assert.Equal(
            $"src/NoTiming.Api/NoTiming.Api.csproj takes the package {package}, and printing is the browser's.",
            violation
        );
    }

    [Fact]
    public void A_project_of_the_tests_takes_Playwright()
    {
        var graph = ProjectGraph.Of(Project("NTS.Tests.Integration", "tests", packages: ["Microsoft.Playwright"]));

        Assert.Empty(ArchitectureRules.OnlyTestsTakePlaywright(graph));
    }

    [Fact]
    public void A_handler_that_asks_the_policy_or_reads_the_session_is_found_and_the_filter_and_the_places_that_tell_are_not()
    {
        var sources = new List<(string Path, string Text)>
        {
            (
                "src/NoTiming.Api/Features/Access/AccessFilter.cs",
                "AccessPolicy.Decide(capability, standing, state); users.GetUserAsync(user);"
            ),
            (
                "src/NoTiming.Api/Features/Events/EventEndpoints.cs",
                "canRun = AccessPolicy.Decide(Capability.RunEvent, standing, state)"
            ),
            (
                "src/NoTiming.Api/Features/Events/Elsewhere.cs",
                "await standing.DecideAtEventAsync(Capability.RunEvent, user, facts, token);"
            ),
            ("src/NoTiming.Api/Features/Profile/Another.cs", "var user = await users.GetUserAsync(context.User);"),
            ("src/NoTiming.Api/Features/Profile/Fine.cs", "var user = access.User;"),
        };

        var violations = ArchitectureRules.OnlyTheFilterAndThePlacesThatTellAskThePolicy(sources);

        Assert.Equal(2, violations.Count);
        Assert.Contains("src/NoTiming.Api/Features/Events/Elsewhere.cs asks the policy", violations[0]);
        Assert.Contains("src/NoTiming.Api/Features/Profile/Another.cs asks the policy", violations[1]);
    }

    [Fact]
    public void A_rule_about_the_endpoint_filter_cannot_pass_in_a_repository_that_has_none()
    {
        var sources = new List<(string Path, string Text)> { ("src/NoTiming.Api/Features/Events/Fine.cs", "") };

        Assert.Throws<InvalidOperationException>(() =>
            ArchitectureRules.OnlyTheFilterAndThePlacesThatTellAskThePolicy(sources)
        );
    }

    /// <summary>A project file with what the rules read from it and nothing else.</summary>
    static (string Path, string Xml) Project(
        string name,
        string folder = "src",
        string target = "net10.0",
        string[]? references = null,
        string[]? packages = null,
        bool azureFunctions = false
    )
    {
        var items = string.Concat(
            (references ?? [])
                .Select(x => $"""<ProjectReference Include="..\{x}\{x}.csproj" />""")
                .Concat((packages ?? []).Select(x => $"""<PackageReference Include="{x}" Version="1.0.0" />"""))
        );
        var functions = azureFunctions ? "<AzureFunctionsVersion>v4</AzureFunctionsVersion>" : "";
        var xml = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{target}</TargetFramework>
                {functions}
              </PropertyGroup>
              <ItemGroup>
                {items}
              </ItemGroup>
            </Project>
            """;
        return ($"{folder}/{name}/{name}.csproj", xml);
    }
}
