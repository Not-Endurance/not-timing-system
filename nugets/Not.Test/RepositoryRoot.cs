namespace Not.Test;

/// <summary>Finds the root of a repository from a folder inside it, by the markers that only the root holds.</summary>
public static class RepositoryRoot
{
    /// <summary>
    /// Walks up from <paramref name="start"/> and returns the first folder that holds every marker. A marker is a file or
    /// a folder name, relative to that folder, such as <c>AGENTS.md</c> or <c>src</c>.
    /// </summary>
    public static string Find(string start, params string[] markers)
    {
        DirectoryInfo? current = new(start);
        while (current is not null)
        {
            if (HoldsEvery(current.FullName, markers))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        var names = string.Join(", ", markers);
        throw new InvalidOperationException($"No folder above {start} holds every one of: {names}.");
    }

    static bool HoldsEvery(string folder, string[] markers)
    {
        foreach (var marker in markers)
        {
            var path = Path.Combine(folder, marker);
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return false;
            }
        }

        return true;
    }
}
