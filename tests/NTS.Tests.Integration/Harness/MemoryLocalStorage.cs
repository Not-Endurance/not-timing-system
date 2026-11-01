using Microsoft.JSInterop;

namespace NTS.Tests.Integration.Harness;

/// <summary>
/// The local storage of a browser as the app reaches it, held in memory: what a Witness keeps of a group that was sent and not
/// answered. A test that stands for a page that is lost and opened again gives the new Witness the same storage.
/// </summary>
internal sealed class MemoryLocalStorage : IJSRuntime
{
    readonly Dictionary<string, string> _items = [];

    /// <summary>What the items are as the browser keeps them.</summary>
    public IReadOnlyDictionary<string, string> Items => _items;

    /// <summary>What every call fails with, as a browser that cannot use its storage does.</summary>
    public Exception? Fails { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (Fails != null)
        {
            return ValueTask.FromException<TValue>(Fails);
        }

        var arguments = args ?? [];
        switch (identifier)
        {
            case "localStorage.getItem":
                return ValueTask.FromResult((TValue)(object?)_items.GetValueOrDefault((string)arguments[0]!)!);
            case "localStorage.setItem":
                _items[(string)arguments[0]!] = (string)arguments[1]!;
                break;
            case "localStorage.removeItem":
                _items.Remove((string)arguments[0]!);
                break;
            case "localStorage.key":
                return ValueTask.FromResult((TValue)(object?)_items.Keys.ElementAtOrDefault((int)arguments[0]!)!);
            default:
                throw new NotSupportedException(identifier);
        }

        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, args);
    }
}
