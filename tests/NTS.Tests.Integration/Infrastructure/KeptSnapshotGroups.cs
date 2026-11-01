using Microsoft.JSInterop;
using Not.Application.CRUD.Ports;
using Not.Blazor.Client.Browser;
using NoTiming.Ui.Features.Core.Dashboard;
using NoTiming.Ui.Storage.Browser;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// What the browser keeps of a group that was sent and not answered, in the local storage that a page reads when it is opened.
/// A test that stands for a page that is lost and opened again gives the new page the same storage.
/// </summary>
sealed class KeptSnapshotGroups
{
    public KeptSnapshotGroups()
    {
        Browser = new InMemoryLocalStorage();
        Repository = new BrowserDurableRepository<DurableSnapshotGroup>(
            new BrowserLocalStorage(Browser),
            new TaskDelay(),
            DurableSnapshotGroup.KEY_PREFIX,
            DurableSnapshotGroup.Options
        );
    }

    public InMemoryLocalStorage Browser { get; }

    public IDurableRepository<DurableSnapshotGroup> Repository { get; }
}

/// <summary>The local storage of a browser as the app reaches it through the JavaScript API: the items are kept in memory.</summary>
sealed class InMemoryLocalStorage : IJSRuntime
{
    readonly Dictionary<string, string> _items = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
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

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args
    )
    {
        return InvokeAsync<TValue>(identifier, args);
    }
}
