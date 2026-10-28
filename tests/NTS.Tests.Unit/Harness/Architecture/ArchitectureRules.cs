using System.Text.RegularExpressions;

namespace NTS.Tests.Unit.Harness.Architecture;

/// <summary>
/// What the layering of the repository allows (ADR-0011, #647). A rule reads the project files and answers with a line for each place
/// that breaks it, which names the project, so that a failure says what to change; a layering that holds is an empty list. A rule that is
/// about a project that the repository lacks throws, so that it cannot hold for the want of something to look at.
/// </summary>
internal static class ArchitectureRules
{
    const string API = "NoTiming.Api";
    const string UI = "NoTiming.Ui";
    static readonly string[] BASE_LIBRARIES = ["Not", "NTS"];
    static readonly string[] INFRASTRUCTURE_PACKAGES =
    [
        "MongoDB.",
        "Microsoft.AspNetCore.",
        "Microsoft.Azure.",
        "Azure.",
        "OpenTelemetry.",
        "Microsoft.Playwright",
        "MudBlazor",
    ];
    static readonly string[] FUNCTIONS_PACKAGES =
    [
        "Microsoft.Azure.Functions",
        "Microsoft.Azure.WebJobs",
        "Microsoft.NET.Sdk.Functions",
    ];
    static readonly string[] RETIRED_PROJECTS = ["NTS.Application", "NTS.Storage", "NTS.Client", "Not.Server"];
    static readonly Regex NAMESPACE_DECLARATION = new(@"^namespace\s+([\w.]+)\s*;", RegexOptions.Multiline);
    static readonly Regex TYPE_DECLARATION = new(
        @"^\s*(?:(?:public|internal|private|protected|sealed|static|readonly|abstract|partial)\s+)*(?:class|record|enum|struct|interface)\s+(\w+)",
        RegexOptions.Multiline
    );
    static readonly Regex TEST_ATTRIBUTE = new(@"^\s*\[(?:Fact|Theory)\]", RegexOptions.Multiline);
    static readonly string[] TEST_LIBRARIES = ["Not.Test", "Not.Test.Tests"];
    static readonly string FILTER = "src/NoTiming.Api/Features/Access/AccessFilter.cs";
    static readonly string[] MAY_ASK_THE_POLICY =
    [
        FILTER,
        "src/NoTiming.Api/Features/Access/StandingReader.cs", // where a caller stands, for the filter and for the places below
        "src/NoTiming.Api/Features/Events/EventEndpoints.cs", // what a person may do about an Event: its capabilities
        "src/NoTiming.Api/Features/Tenancy/TenantEndpoints.cs", // what a person may do in a Tenant: its capabilities
        "src/NoTiming.Api/Features/EventData/TimeEventViewers.cs", // whether a read shows how the times were recorded
        "src/NoTiming.Api/Features/Tenancy/CrossTenantReads.cs", // the second guard behind the reads that reach across Tenants
    ];
    static readonly Regex ASKS_THE_POLICY = new(
        @"AccessPolicy\.Decide\(|\.DecideAt(Event|Tenant|Platform)Async\(|\.GetUserAsync\(",
        RegexOptions.Compiled
    );

    /// <summary>
    /// What a caller may do is asked of the policy by the endpoint filter, and by no handler (ADR-0012, #671): a route declares what
    /// it needs with <c>[Requires]</c> and takes the caller from the context the filter filled. The sources of the Api that ask
    /// the policy or read the session are the filter and the few places that do not authorize a request: the capabilities, what a
    /// read of times shows, and the reads that reach across Tenants, which ask again as a second guard. A rule about a filter that
    /// the repository lacks throws, so that it cannot hold for the want of something to look at.
    /// </summary>
    public static IReadOnlyList<string> OnlyTheFilterAndThePlacesThatTellAskThePolicy(
        IReadOnlyList<(string Path, string Text)> sources
    )
    {
        if (!sources.Any(x => x.Path == FILTER))
        {
            throw new InvalidOperationException("The repository has no endpoint filter.");
        }

        return
        [
            .. sources
                .Where(x => !MAY_ASK_THE_POLICY.Contains(x.Path) && ASKS_THE_POLICY.IsMatch(x.Text))
                .Select(x =>
                    $"{x.Path} asks the policy or reads the session, and only the endpoint filter and the places that tell do: declare what the route needs with [Requires]."
                ),
        ];
    }

    /// <summary>The Ui is what the browser runs and the Api serves it: the Ui never references the Api, directly or through a project it references.</summary>
    public static IReadOnlyList<string> UiNeverReferencesApi(ProjectGraph graph)
    {
        var ui = graph.Get(UI);

        return graph.Closure(ui.Name).Any(x => x.Name == API)
            ? [$"{UI} references {API}, which serves it and so must not be known to it."]
            : [];
    }

    /// <summary>
    /// The domain stands on the rest of the domain and the base libraries (<c>Not</c> and <c>NTS</c>), and takes no package of what it
    /// must not know: a database, a web host, a cloud, telemetry, a browser to drive or a component library (by the start of the id).
    /// </summary>
    public static IReadOnlyList<string> DomainReferencesNoInfrastructure(ProjectGraph graph)
    {
        var domain = graph.Projects.Where(IsDomain).ToList();
        if (domain.Count == 0)
        {
            throw new InvalidOperationException("The repository has no domain project.");
        }

        var violations = new List<string>();
        foreach (var project in domain)
        {
            foreach (var reached in graph.Closure(project.Name))
            {
                if (!IsDomain(reached) && !BASE_LIBRARIES.Contains(reached.Name))
                {
                    violations.Add($"{project.Name} reaches {reached.Name}, which is not part of the domain.");
                }

                violations.AddRange(
                    reached
                        .Packages.Where(x => StartsWithAny(x, INFRASTRUCTURE_PACKAGES))
                        .Select(x =>
                            $"{project.Name} reaches the package {x} in {reached.Name}, which the domain must not know."
                        )
                );
            }
        }

        return violations;
    }

    /// <summary>
    /// The Functions API retired, and with it the runtime it ran on: no project is a Functions app or takes a package of one
    /// (<c>Microsoft.Azure.Functions</c>, <c>Microsoft.Azure.WebJobs</c>, <c>Microsoft.NET.Sdk.Functions</c>).
    /// </summary>
    public static IReadOnlyList<string> NothingReferencesAzureFunctions(ProjectGraph graph)
    {
        var violations = new List<string>();
        foreach (var project in graph.Projects)
        {
            if (project.IsAzureFunctions)
            {
                violations.Add($"{project.Path} is an Azure Functions app.");
            }

            violations.AddRange(
                project
                    .Packages.Where(x => StartsWithAny(x, FUNCTIONS_PACKAGES))
                    .Select(x => $"{project.Path} takes the package {x}.")
            );
        }

        return violations;
    }

    /// <summary>The libraries moved to .NET 10 with the hosts (ADR-0011): a project that targets another version, or names none, is the one that was left behind.</summary>
    public static IReadOnlyList<string> EveryProjectIsOnNet10(ProjectGraph graph)
    {
        return
        [
            .. graph
                .Projects.Where(x => x.Targets.Count == 0 || !x.Targets.All(IsNet10))
                .Select(x => $"{x.Path} targets '{string.Join(';', x.Targets)}' and not only net10.0."),
        ];
    }

    /// <summary>
    /// A project that retired with the Functions API does not come back, which an IDE that keeps the solution it had can do:
    /// <c>NTS.Application</c>, <c>NTS.Storage</c>, <c>NTS.Client</c>, <c>Not.Server</c> and the <c>NTS.Nexus</c> ones, whose code
    /// is in the Ui and the Api now.
    /// </summary>
    public static IReadOnlyList<string> RetiredProjectsStayGone(ProjectGraph graph)
    {
        return
        [
            .. graph
                .Projects.Where(x =>
                    RETIRED_PROJECTS.Contains(x.Name) || x.Name.StartsWith("NTS.Nexus", StringComparison.Ordinal)
                )
                .Select(x => $"{x.Path} is a project that retired with the Functions API."),
        ];
    }

    /// <summary>Printing is the browser's: a project of the product takes no Playwright, which only the browser tests drive.</summary>
    public static IReadOnlyList<string> OnlyTestsTakePlaywright(ProjectGraph graph)
    {
        return
        [
            .. graph
                .Projects.Where(x => !x.Path.StartsWith("tests/", StringComparison.Ordinal) && !TEST_LIBRARIES.Contains(x.Name))
                .SelectMany(x =>
                    x.Packages.Where(y => y.StartsWith("Microsoft.Playwright", StringComparison.OrdinalIgnoreCase))
                        .Select(y => $"{x.Path} takes the package {y}, and printing is the browser's.")
                ),
        ];
    }

    /// <summary>
    /// A unit test sits in the folder of the product code it covers, and declares the namespace of that folder (ADR-0016): a test in a
    /// folder that the product does not have tests nothing that is there. The harness and the architecture tests are not tests of a
    /// folder of the product, and are left out.
    /// </summary>
    public static IReadOnlyList<string> TestsMirrorTheProduct(
        IReadOnlyList<(string Path, string Text)> testFiles,
        IReadOnlyCollection<string> productFolders
    )
    {
        var violations = new List<string>();
        foreach (var (path, text) in testFiles)
        {
            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
            if (IsOutsideTheMirror(folder))
            {
                continue;
            }

            if (folder.Length == 0 || !productFolders.Contains(folder))
            {
                violations.Add($"{path} is in a folder that the product does not have.");
                continue;
            }

            var expected = "NTS.Tests.Unit." + folder.Replace('/', '.');
            var declared = NAMESPACE_DECLARATION.Match(text).Groups[1].Value;
            if (declared != expected)
            {
                violations.Add($"{path} declares the namespace {declared}, not {expected}.");
            }
        }

        return violations;
    }

    /// <summary>A test file declares tests and nothing else: a double, a fixture or a model that a test uses belongs in the harness.</summary>
    public static IReadOnlyList<string> TestFilesDeclareOnlyTests(IReadOnlyList<(string Path, string Text)> testFiles)
    {
        var violations = new List<string>();
        foreach (var (path, text) in testFiles)
        {
            foreach (Match declaration in TYPE_DECLARATION.Matches(text))
            {
                var name = declaration.Groups[1].Value;
                if (!name.EndsWith("Tests", StringComparison.Ordinal))
                {
                    violations.Add($"{path} declares {name}, which is not a test: move it to the harness.");
                }
            }
        }

        return violations;
    }

    /// <summary>The harness is support for the tests, and holds no test of its own.</summary>
    public static IReadOnlyList<string> HarnessHoldsNoTests(IReadOnlyList<(string Path, string Text)> harnessFiles)
    {
        var violations = new List<string>();
        foreach (var (path, text) in harnessFiles)
        {
            if (TEST_ATTRIBUTE.IsMatch(text))
            {
                violations.Add($"{path} holds a test, and the harness holds none.");
            }
        }

        return violations;
    }

    /// <summary>The shared library of the tests is generic (ADR-0016): it references no project of the product.</summary>
    public static IReadOnlyList<string> NotTestReferencesNoProduct(ProjectGraph graph)
    {
        var library = graph.Get("Not.Test");
        return
        [
            .. library
                .Projects.Where(x => x.StartsWith("NTS", StringComparison.Ordinal) || x.StartsWith("NoTiming", StringComparison.Ordinal))
                .Select(x => $"{library.Path} references {x}, a project of the product."),
        ];
    }

    static bool IsOutsideTheMirror(string folder)
    {
        return folder == "Harness"
            || folder.StartsWith("Harness/", StringComparison.Ordinal)
            || folder == "Architecture"
            || folder.StartsWith("Architecture/", StringComparison.Ordinal);
    }

    static bool IsDomain(ProjectFile project)
    {
        return project.Name == "Not.Domain" || project.Name.StartsWith("NTS.Domain", StringComparison.Ordinal);
    }

    static bool IsNet10(string target)
    {
        return target == "net10.0" || target.StartsWith("net10.0-", StringComparison.Ordinal);
    }

    static bool StartsWithAny(string package, string[] prefixes)
    {
        return prefixes.Any(x => package.StartsWith(x, StringComparison.OrdinalIgnoreCase));
    }
}
