using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Not.Concurrency;
using Not.Filesystem;
using Not.Storage.States;

namespace Not.Storage.Stores.Files;

public class LockingJsonFileStore<T> : IStore<T>
    where T : class, IState, new()
{
    readonly TimeoutLockSemaphore _timeoutLock;
    readonly string _path;

    public LockingJsonFileStore([FromKeyedServices(StoreConstants.DATA_KEY)] IFileContext configuration)
    {
        _path = Path.Combine(configuration.Path, $"{typeof(T).Name}.json");
        _timeoutLock = new TimeoutLockSemaphore();
    }

    public async Task<T> Readonly(
        [CallerFilePath] string callerPath = default!,
        [CallerMemberName] string callerMember = default!
    )
    {
        // TODO: figure out better way? Maybe cache the contents and update the cache on Commit?
        var transactionId = await _timeoutLock.Wait(callerPath, callerMember);
        var state = await JsonFileStore.Read<T>(_path) ?? new T();
        _timeoutLock.Release(transactionId);
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
}
