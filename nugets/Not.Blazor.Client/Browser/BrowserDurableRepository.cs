using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.JSInterop;
using Not.Application.CRUD.Ports;

namespace Not.Blazor.Client.Browser;

/// <summary>
/// A durable repository whose backup is kept in the local storage of the browser, one entry per id under the prefix (see
/// <see cref="IDurableRepository{T}"/>). An item with no remote repository is kept only as its backup: its plain operations throw
/// <see cref="NotImplementedException"/>, as there is no remote to ask, and a read finds nothing but the backup.
/// </summary>
public sealed class BrowserDurableRepository<T> : IDurableRepository<T>
{
    readonly IRepository<T>? _remote;
    readonly ILocalStorage _backup;
    readonly string _prefix;
    readonly JsonSerializerOptions _options;

    public BrowserDurableRepository(
        IRepository<T>? remote,
        ILocalStorage backup,
        string prefix,
        JsonSerializerOptions options
    )
    {
        _remote = remote;
        _backup = backup;
        _prefix = prefix;
        _options = options;
    }

    public async Task<bool> StoreDurably(Guid id, T item, Func<T, Task<bool>> send)
    {
        await _backup.SetItem(KeyOf(id), JsonSerializer.Serialize(item, _options));
        var final = await send(item);
        if (final)
        {
            // Removing the backup is best effort once the server has answered: a backup that stays is sent again as the same group,
            // and the server answers it with its first outcome.
            try
            {
                await _backup.RemoveItem(KeyOf(id));
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
                // The browser refuses to remove it: see above.
            }
        }

        return final;
    }

    public async Task RemoveDurably(Guid id, T item)
    {
        await Remote().Delete(item);
        await _backup.RemoveItem(KeyOf(id));
    }

    public async Task<T?> ReadDurably(Guid id)
    {
        var text = await _backup.GetItem(KeyOf(id));
        if (text != null)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(text, _options);
            }
            catch (JsonException)
            {
                await _backup.RemoveItem(KeyOf(id)); // what is kept is not an item, and would not be read the next time either
            }
        }

        return _remote == null ? default : await _remote.Read(id);
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

    public Task Create(T item)
    {
        return Remote().Create(item);
    }

    public Task<T?> Read(Guid id)
    {
        return Remote().Read(id);
    }

    public Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        return Remote().Read(filter);
    }

    public Task<IEnumerable<T>> ReadMany()
    {
        return Remote().ReadMany();
    }

    public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        return Remote().ReadMany(filter);
    }

    public Task Update(T item)
    {
        return Remote().Update(item);
    }

    public Task Delete(T item)
    {
        return Remote().Delete(item);
    }

    public Task DeleteMany(IEnumerable<T> items)
    {
        return Remote().DeleteMany(items);
    }

    public Task DeleteMany(Expression<Func<T, bool>> filter)
    {
        return Remote().DeleteMany(filter);
    }

    string KeyOf(Guid id)
    {
        return _prefix + id.ToString("D");
    }

    IRepository<T> Remote()
    {
        return _remote
            ?? throw new NotImplementedException("This item has no remote repository: only its backup is kept.");
    }
}
