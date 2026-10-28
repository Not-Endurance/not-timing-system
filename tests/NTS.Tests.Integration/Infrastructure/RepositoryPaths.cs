using Not.Test;

namespace NTS.Tests.Integration.Infrastructure;

internal sealed class RepositoryPaths
{
    public static RepositoryPaths Discover()
    {
        return new RepositoryPaths(RepositoryRoot.Find(AppContext.BaseDirectory, "AGENTS.md", "src"));
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
}
