using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain.Aggregates;
using Not.Storage.JsonFile.States;
using Not.Storage.JsonFile.Stores;

namespace Not.Storage.JsonFile.Repositories;

public abstract class ReadonlySetRepository<T, TState> : IRead<T>
    where T : Aggregate
    where TState : class, ISetState<T>, new()
{
    protected ReadonlySetRepository(IStore<TState> store)
    {
        Store = store;
    }

    protected IStore<TState> Store { get; }

    public async Task<T?> Read(int id)
    {
        var state = await Store.Readonly();
        return state.EntitySet.FirstOrDefault(x => x.Id == id);
    }

    public async Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        var state = await Store.Readonly();
        var predicate = filter.Compile();
        return state.EntitySet.FirstOrDefault(x => predicate(x));
    }

    public async Task<IEnumerable<T>> ReadMany()
    {
        var state = await Store.Readonly();
        return state.EntitySet;
    }

    public async Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        var state = await Store.Readonly();
        var predicate = filter.Compile();
        return state.EntitySet.Where(x => predicate(x));
    }
}
