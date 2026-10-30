namespace Not.Application.Environments;

/// <summary>
/// A storage was marked as another environment than the one that was asked for: a marker is not changed to another name
/// (a storage that is production stays so until somebody removes the marker by hand).
/// </summary>
public sealed class EnvironmentMarkedException : InvalidOperationException
{
    public EnvironmentMarkedException(string existing)
        : base($"The database is marked as '{existing}', and a marker is not changed to another name.")
    {
        Existing = existing;
    }

    /// <summary>The name the storage is marked with.</summary>
    public string Existing { get; }
}

/// <summary>
/// What a storage says it is (#607): which environment its data belongs to. Production, staging and a developer's own
/// storage hold the same collections, so nothing else can tell them apart, and the commands and routes that must never
/// touch a production storage look here first. It is written by the commands that prepare a storage and is never changed
/// to another name. The names, and what each of them allows, are <see cref="StorageEnvironmentNames"/>; the environment the
/// running host is in is <see cref="IEnvironmentContext"/>, which is a different question.
/// </summary>
public interface IStorageEnvironment
{
    /// <summary>
    /// The name the storage is marked with, as it is stored, or none when it has no marker. A name that is not one of an
    /// environment is told as it is: the storage is not taken for another one because its marker is misspelt.
    /// </summary>
    Task<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the storage. True when the marker was written by this call, false when the storage already says the same name,
    /// and an <see cref="EnvironmentMarkedException"/> when it says another: of several writes at once exactly one writes.
    /// The name has to be one of an environment (<see cref="StorageEnvironmentNames.Canonical"/>), or an
    /// <see cref="ArgumentException"/> is thrown, and is stored as it is written there.
    /// </summary>
    Task<bool> WriteAsync(string name, DateTimeOffset at, CancellationToken cancellationToken = default);
}
