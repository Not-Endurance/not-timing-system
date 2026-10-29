using System.Text.Json;
using Not.Application.CRUD.Ports;
using Not.Async;

namespace Not.Blazor.Client.Browser;

/// <summary>
/// A durable repository whose backup is kept in the local storage of the browser, one entry per id under the prefix (see
/// <see cref="IDurableRepository{T}"/>). The retries wait five seconds at first, then longer, up to a minute.
/// </summary>
public sealed class BrowserDurableRepository<T> : IDurableRepository<T>
{
    static readonly TimeSpan FIRST_RETRY = TimeSpan.FromSeconds(5);
    static readonly TimeSpan LONGEST_RETRY = TimeSpan.FromSeconds(60);

    readonly ILocalStorage _backup;
    readonly IDelay _delay;
    readonly string _prefix;
    readonly JsonSerializerOptions _options;

    public BrowserDurableRepository(ILocalStorage backup, IDelay delay, string prefix, JsonSerializerOptions options)
    {
        _backup = backup;
        _delay = delay;
        _prefix = prefix;
        _options = options;
    }

    public async Task<bool> StoreDurably(Guid id, T item, Func<T, Task<bool>> send)
    {
        await TryHold(id, item);
        var final = await send(item);
        if (final)
        {
            await TryRemove(id);
        }

        return final;
    }

    public async Task<IReadOnlyList<T>> ReadPendingDurably(Func<T, bool> match)
    {
        var pending = new List<T>();
        foreach (var key in (await _backup.GetKeys()).Where(x => x.StartsWith(_prefix, StringComparison.Ordinal)))
        {
            var text = await _backup.GetItem(key);
            if (text == null)
            {
                continue;
            }

            try
            {
                if (JsonSerializer.Deserialize<T>(text, _options) is { } item && match(item))
                {
                    pending.Add(item);
                }
            }
            catch (JsonException)
            {
                await _backup.RemoveItem(key); // what is kept is not an item, and would not be read the next time either
            }
        }

        return pending;
    }

    public Task KeepDurably(Guid id, T item)
    {
        return _backup.SetItem(KeyOf(id), JsonSerializer.Serialize(item, _options));
    }

    public Task ForgetDurably(Guid id)
    {
        return _backup.RemoveItem(KeyOf(id));
    }

    public Task RetryDurably(Func<bool> canRetry, Func<CancellationToken, Task<bool>> round, CancellationToken stop)
    {
        return RetryUntilDone.Run(_delay, new Retry(canRetry, round), FIRST_RETRY, LONGEST_RETRY, stop);
    }

    /// <summary>
    /// The backup is written before the remote is asked. A browser that refuses its storage, in any way, does not hold the request
    /// back: the request goes on without a backup.
    /// </summary>
    async Task TryHold(Guid id, T item)
    {
        try
        {
            await _backup.SetItem(KeyOf(id), JsonSerializer.Serialize(item, _options));
        }
        catch (Exception)
        {
            // The browser refuses its storage: the request is made all the same, and is kept only as far as the caller keeps it.
        }
    }

    async Task TryRemove(Guid id)
    {
        try
        {
            await _backup.RemoveItem(KeyOf(id));
        }
        catch (Exception)
        {
            // Removing the backup is best effort once the server has answered: a backup that stays is sent again as the same group,
            // and the server answers it with its first outcome.
        }
    }

    string KeyOf(Guid id)
    {
        return _prefix + id.ToString("D");
    }

    /// <summary>The round as the retries see it: whether it may go on, and one more round.</summary>
    sealed class Retry : IRetryable
    {
        readonly Func<bool> _canRetry;
        readonly Func<CancellationToken, Task<bool>> _round;

        public Retry(Func<bool> canRetry, Func<CancellationToken, Task<bool>> round)
        {
            _canRetry = canRetry;
            _round = round;
        }

        public bool CanRetry => _canRetry();

        public Task<bool> RetryAsync(CancellationToken cancellationToken)
        {
            return _round(cancellationToken);
        }
    }
}
