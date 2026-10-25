namespace NTS.Tests.Integration.Infrastructure;

internal sealed class RepositoryPaths
{
    public static RepositoryPaths Discover()
    {
        return Discover(AppContext.BaseDirectory);
    }

    RepositoryPaths(string root)
    {
        Root = root;
        DotnetHome = Path.Combine(root, ".tmp", "integration-dotnet-home");
        NuGetPackages = Path.Combine(root, ".tmp", "integration-nuget-packages");
    }

    public string Root { get; }
    public string DotnetHome { get; }
    public string NuGetPackages { get; }

    static RepositoryPaths Discover(string baseDirectory)
    {
        var current = new DirectoryInfo(baseDirectory);
        while (current != null)
        {
            var agentsFile = Path.Combine(current.FullName, "AGENTS.md");
            var srcDirectory = Path.Combine(current.FullName, "src");
            if (File.Exists(agentsFile) && Directory.Exists(srcDirectory))
            {
                return new RepositoryPaths(current.FullName);
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the not-timing-system repository root.");
    }
}
