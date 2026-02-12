using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Not.Async;
using Not.Filesystem;
using Not.Storage.JsonFile.States;

namespace Not.Storage.JsonFile.Stores.Files;

public class LockingJsonFileStore<T> : IStore<T>
    where T : class, IState, new()
{
    readonly TimeoutLockSemaphore _timeoutLock;
    readonly string _path;

    public LockingJsonFileStore([FromKeyedServices(StoreConstants.DATA_KEY)] IFilesystemContext configuration)
    {
        _path = Path.Combine(configuration.AppDirectory, $"{typeof(T).Name}.json");
        _timeoutLock = new TimeoutLockSemaphore();
    }

    public async Task<T> Readonly(
        [CallerFilePath] string callerPath = default!,
        [CallerMemberName] string callerMember = default!
    )
    {
        var state = await JsonFileStore.Read<T>(_path) ?? new T();
        return state;
    }

    public async Task<T> Transact(
        [CallerFilePath] string callerPath = default!,
        [CallerMemberName] string callerMember = default!
    )
    {
        var transactionId = await _timeoutLock.Wait(callerPath, callerMember);

        var state = await JsonFileStore.Read<T>(_path) ?? new T();
        state.TransactionId = transactionId;
        return state;
    }

    public async Task Commit(T state)
    {
        if (state.TransactionId == null)
        {
            throw new Exception(
                $"Cannot commit state without a transaction. Please use '{nameof(Transact)}' for write operations"
            );
        }

        await JsonFileStore.Write(_path, state);
        _timeoutLock.Release(state.TransactionId.Value);
    }

    public async Task Delete()
    {
        await JsonFileStore.BackupAndDelete(_path);
    }
}
