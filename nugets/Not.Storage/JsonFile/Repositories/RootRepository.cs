using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain.Aggregates;
using Not.Storage.JsonFile.States;
using Not.Storage.JsonFile.Stores;

namespace Not.Storage.JsonFile.Repositories;

/// <summary>
/// Represent a set of operations for root-level entiries in a Tree-like data structure.
/// Implements IReposistory to allow for streamline API, but does not support any Delete operations
/// or Read(Predicate) operations as they are not necessary.
/// </summary>
/// <typeparam name="T">Type of the Root entity</typeparam>
/// <typeparam name="TState">Type of the state object containing the Root entity</typeparam>
public abstract class RootRepository<T, TState> : ReadonlyRootRepository<T, TState>, IRepository<T>
    where T : Aggregate
    where TState : class, ITreeState<T>, new()
{
    protected RootRepository(IStore<TState> store)
        : base(store) { }

    public async Task Create(T item)
    {
        var state = await Store.Transact();
        state.Root = item;
        await Store.Commit(state);
    }

    public async Task Update(T items)
    {
        await Create(items);
    }

    public Task Delete(int id)
    {
        throw NotImplemented();
    }

    public Task Delete(Expression<Func<T, bool>> filter)
    {
        throw NotImplemented();
    }

    public Task Delete(T item)
    {
        throw NotImplemented();
    }

    public Task Delete(IEnumerable<T> items)
    {
        throw NotImplemented();
    }
}
