using System.Collections.Concurrent;

namespace NTS.Tests.Integration.Infrastructure;

internal sealed class ProcessOutputCollector
{
    readonly ConcurrentQueue<string> _lines = new();

    public void Add(string stream, string? line)
    {
        if (line == null)
        {
            return;
        }

        _lines.Enqueue($"{DateTimeOffset.UtcNow:O} [{stream}] {line}");
    }

    public string Dump()
    {
        return string.Join(Environment.NewLine, _lines);
    }
}
