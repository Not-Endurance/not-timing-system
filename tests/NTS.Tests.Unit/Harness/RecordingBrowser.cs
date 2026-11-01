using Microsoft.JSInterop;

namespace NTS.Tests.Unit.Harness;

/// <summary>A browser that remembers the last call it was asked for, and answers nothing.</summary>
internal sealed class RecordingBrowser : IJSRuntime
{
    public string? Identifier { get; private set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        Identifier = identifier;
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
