using Not.Storage.Ports.States;
using System.Runtime.CompilerServices;

namespace Not.Storage.Stores;

public interface IStore<T>
    where T : class, IState, new()
{
    public Task<T> Readonly();
    public Task<T> Transact([CallerFilePath] string callerPath = default!, [CallerMemberName] string callerMember = default!);
    public Task Commit(T state);
}