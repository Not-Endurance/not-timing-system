using System.Collections.Concurrent;

namespace NoTiming.Api.Application.Features.Participations;

/// <summary>
/// Serialises participation writes per event within this process. A command reads the current state, applies its
/// rule and saves, so two commands for the same event must not interleave. The API runs as a single instance.
/// </summary>
public sealed class EventWriteLocks
{
    readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public async Task<T> Run<T>(int eventId, Func<Task<T>> work)
    {
        var gate = _locks.GetOrAdd(eventId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            return await work();
        }
        finally
        {
            gate.Release();
        }
    }

    public Task Run(int eventId, Func<Task> work)
    {
        return Run(
            eventId,
            async () =>
            {
                await work();
                return true;
            }
        );
    }
}
