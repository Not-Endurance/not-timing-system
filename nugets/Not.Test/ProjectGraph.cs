using System.Xml.Linq;

namespace Not.Test;

/// <summary>One project of a repository as its project file says it: what it targets, what it references, and the packages it takes.</summary>
public sealed class ProjectFile
{
    public ProjectFile(
        string name,
        string path,
        IReadOnlyList<string> projects,
        IReadOnlyList<string> packages,
        IReadOnlyList<string> targets,
        bool isAzureFunctions
    )
    {
        Name = name;
        Path = path;
        Projects = projects;
        Packages = packages;
        Targets = targets;
        IsAzureFunctions = isAzureFunctions;
    }

    /// <summary>The name of the project: its file name without the extension.</summary>
    public string Name { get; }

    /// <summary>The path of the project file, from the root of the repository, with forward slashes.</summary>
    public string Path { get; }

    /// <summary>The names of the projects it references.</summary>
    public IReadOnlyList<string> Projects { get; }

    /// <summary>The packages it takes, by id.</summary>
    public IReadOnlyList<string> Packages { get; }

    /// <summary>The target frameworks it names.</summary>
    public IReadOnlyList<string> Targets { get; }

    /// <summary>Whether the project says it is an Azure Functions app.</summary>
    public bool IsAzureFunctions { get; }
}

/// <summary>
/// The projects of a repository and what references what, read from the project files and not from a build, so that a layering can be
/// checked by anyone who can read the repository. The rules over it belong to the repository that reads it.
/// </summary>
public sealed class ProjectGraph
{
    static readonly string[] BUILD_OUTPUT = ["bin", "obj", "node_modules"];

    /// <summary>The projects of the repository in the folders given, below its root. A folder the repository lacks is left out.</summary>
    public static ProjectGraph Load(string root, params string[] folders)
    {
        var files = folders
            .Select(x => Path.Combine(root, x))
            .Where(Directory.Exists)
            .SelectMany(ProjectFilesUnder)
            .Select(x => Of(Path.GetRelativePath(root, x).Replace('\\', '/'), File.ReadAllText(x)));
        return new ProjectGraph(files);
    }

    /// <summary>A graph of the project files given, by their path and their XML, for a test of a rule.</summary>
    public static ProjectGraph Of(params (string Path, string Xml)[] projects)
    {
        return new ProjectGraph(projects.Select(x => Of(x.Path, x.Xml)));
    }

    readonly Dictionary<string, ProjectFile> _projects;

    ProjectGraph(IEnumerable<ProjectFile> projects)
    {
        Projects = [.. projects];
        _projects = Projects.ToDictionary(x => x.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<ProjectFile> Projects { get; }

    public ProjectFile Get(string name)
    {
        return _projects.TryGetValue(name, out var project)
            ? project
            : throw new InvalidOperationException($"The repository has no project called {name}.");
    }

    public bool Has(string name)
    {
        return _projects.ContainsKey(name);
    }

    /// <summary>The project and every project it references, directly or through another.</summary>
    public IReadOnlyList<ProjectFile> Closure(string name)
    {
        var found = new Dictionary<string, ProjectFile>(StringComparer.Ordinal);
        var pending = new Stack<string>([name]);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (found.ContainsKey(current) || !_projects.TryGetValue(current, out var project))
            {
                continue;
            }

            found[current] = project;
            foreach (var reference in project.Projects)
            {
                pending.Push(reference);
            }
        }

        return [.. found.Values];
    }

    static ProjectFile Of(string path, string xml)
    {
        var elements = XDocument.Parse(xml).Descendants().ToList();
        string[] Values(string element, string attribute)
        {
            return
            [
                .. elements
                    .Where(x => x.Name.LocalName == element)
                    .Select(x => (string?)x.Attribute(attribute))
                    .OfType<string>(),
            ];
        }

        var targets = elements
            .Where(x => x.Name.LocalName is "TargetFramework" or "TargetFrameworks")
            .SelectMany(x =>
                x.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            );
        return new ProjectFile(
            Path.GetFileNameWithoutExtension(path),
            path,
            [.. Values("ProjectReference", "Include").Select(NameOf)],
            Values("PackageReference", "Include"),
            [.. targets],
            elements.Any(x => x.Name.LocalName == "AzureFunctionsVersion")
        );
    }

    // A project file names the other with the slashes of whoever wrote it, which Path reads only for the operating system it runs on.
    static string NameOf(string path)
    {
        return Path.ChangeExtension(path.Split('/', '\\')[^1], null);
    }

    // The folders of the repository are walked and the output of a build is not: it holds copies of projects, and tests that run
    // beside this one write and delete files in it while it is read (a walk that went through it could fail on a folder that is gone).
    static IEnumerable<string> ProjectFilesUnder(string folder)
    {
        return Directory
            .EnumerateFiles(folder, "*.csproj")
            .Concat(
                Directory
                    .EnumerateDirectories(folder)
                    .Where(x => !BUILD_OUTPUT.Contains(Path.GetFileName(x)))
                    .SelectMany(ProjectFilesUnder)
            );
    }
}
