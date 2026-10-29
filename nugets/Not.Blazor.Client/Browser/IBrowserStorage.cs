namespace Not.Blazor.Client.Browser;

/// <summary>
/// A key-value store of the browser. A call that the browser refuses throws, and the caller decides what that means: a store that
/// cannot be used is not the same as one that is empty.
/// </summary>
public interface IBrowserStorage
{
    Task<string?> GetItem(string key);

    Task SetItem(string key, string value);

    Task RemoveItem(string key);

    /// <summary>The keys the store holds, in the order the browser lists them.</summary>
    Task<IReadOnlyList<string>> GetKeys();
}
