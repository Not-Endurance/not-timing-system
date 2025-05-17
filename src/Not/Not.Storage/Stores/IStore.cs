using System.Runtime.CompilerServices;
using Not.Storage.States;

namespace Not.Storage.Stores;

public interface IStore<T>
    where T : class, IState, new()
{
    public Task<T> Readonly(
        [CallerFilePath] string callerPath = default!,
        [CallerMemberName] string callerMember = default!
    );
    public Task<T> Transact(
        [CallerFilePath] string callerPath = default!,
        [CallerMemberName] string callerMember = default!
    );
    public Task Commit(T state);
    public Task Delete();
}
