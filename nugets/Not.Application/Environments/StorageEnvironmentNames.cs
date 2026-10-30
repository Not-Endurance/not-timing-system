namespace Not.Application.Environments;

/// <summary>
/// The names a storage can say it is (<see cref="IStorageEnvironment"/>) and what each of them allows. They are the names
/// of <see cref="NEnvironmentNames"/>, but they are read strictly: <see cref="NEnvironmentNames.Normalize"/> takes a host with
/// no name for production, whereas a storage with no name, or with a name that is none of an environment, is not told to be
/// one.
/// </summary>
public static class StorageEnvironmentNames
{
    /// <summary>The name of the environment a person typed, in any case, as it is written; none for anything else.</summary>
    public static string? Canonical(string? typed)
    {
        var name = typed?.Trim();
        return Names.FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether the name says production, in any case. Only that name does: a storage with no name is not told to be one.</summary>
    public static bool IsProduction(string? name)
    {
        return string.Equals(name?.Trim(), NEnvironmentNames.PRODUCTION, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether the name says Staging or Development, in any case: the only names that let what must never touch a production
    /// storage work on this one. No name does not, Production does not, and nor does a name that is none of an environment (a
    /// marker written by hand and misspelt): a storage is worked on as a developer's only when it says that it is one.
    /// </summary>
    public static bool IsNonProduction(string? name)
    {
        return Canonical(name) is NEnvironmentNames.STAGING or NEnvironmentNames.DEVELOPMENT;
    }

    /// <summary>The environments a storage can be, as they are written.</summary>
    public static IReadOnlyList<string> Names { get; } =
        [NEnvironmentNames.PRODUCTION, NEnvironmentNames.STAGING, NEnvironmentNames.DEVELOPMENT];
}
