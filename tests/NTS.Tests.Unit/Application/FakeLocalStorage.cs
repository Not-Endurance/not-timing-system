using Microsoft.JSInterop;

namespace NTS.Tests.Unit.Application;

/// <summary>The local storage of a browser as the app reaches it, which can be made to refuse.</summary>
internal sealed class FakeLocalStorage : IJSRuntime
{
    readonly Dictionary<string, string> _items = [];

    /// <summary>What the app asked of the browser, in order.</summary>
    public List<(string Identifier, object?[] Arguments)> Calls { get; } = [];

    /// <summary>What the items are as the browser keeps them.</summary>
    public IReadOnlyDictionary<string, string> Items => _items;

    /// <summary>What every call fails with, as a browser that cannot use its storage does.</summary>
    public Exception? Fails { get; set; }

    public void Put(string key, string value)
    {
        _items[key] = value;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (Fails != null)
        {
            return ValueTask.FromException<TValue>(Fails);
        }

        var arguments = args ?? [];
        Calls.Add((identifier, arguments));
        var key = (string)arguments[0]!;
        switch (identifier)
        {
            case "localStorage.getItem":
                return ValueTask.FromResult((TValue)(object?)_items.GetValueOrDefault(key)!);
            case "localStorage.setItem":
                _items[key] = (string)arguments[1]!;
                break;
            case "localStorage.removeItem":
                _items.Remove(key);
                break;
            default:
                throw new NotSupportedException(identifier);
        }

        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args
    )
    {
        return InvokeAsync<TValue>(identifier, args);
    }
}
