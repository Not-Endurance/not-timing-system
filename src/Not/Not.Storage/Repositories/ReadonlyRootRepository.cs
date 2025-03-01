using System.Linq.Expressions;
using Not.Domain.Base;
using Not.Storage.States;

namespace Not.Storage.Repositories;

public abstract class ReadonlyRootRepository<T, TState>
    where T : AggregateRoot
    where TState : class, ITreeState<T>, new()
{
    protected ReadonlyRootRepository(IStore<TState> store)
    {
        Store = store;
    }

    protected IStore<TState> Store { get; }

    protected ApplicationException NotImplemented()
    {
        return new ApplicationException(
            "Only Create, Read and Update operations are implemented for Root entities."
        );
    }

    public async Task<T?> Read(Expression<Func<T, bool>> _)
    {
        return await Read(0);
    }

    public async Task<T?> Read(int _)
    {
        var state = await Store.Readonly();
        return state.Root;
    }

    public Task<IEnumerable<T>> ReadAll()
    {
        throw NotImplemented();
    }

    public Task<IEnumerable<T>> ReadAll(Expression<Func<T, bool>> filter)
    {
        throw NotImplemented();
    }
}
