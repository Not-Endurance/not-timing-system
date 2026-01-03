using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain.Aggregates;
using Not.Storage.States;

namespace Not.Storage.Repositories;

public abstract class SetRepository<T, TState> : ReadonlySetRepository<T, TState>, IRepository<T>
    where T : AggregateRoot
    where TState : class, ISetState<T>, new()
{
    public SetRepository(IStore<TState> store)
        : base(store) { }

    public async Task Create(T item)
    {
        var state = await Store.Transact();
        state.EntitySet.Add(item);
        await Store.Commit(state);
    }

    public async Task Delete(int id)
    {
        var state = await Store.Transact();
        state.EntitySet.RemoveAll(x => x.Id == id);
        await Store.Commit(state);
    }

    public async Task Delete(Expression<Func<T, bool>> filter)
    {
        var state = await Store.Transact();
        var predicate = filter.Compile();
        state.EntitySet.RemoveAll(x => predicate(x));
        await Store.Commit(state);
    }

    public virtual async Task Delete(T item)
    {
        var state = await Store.Transact();
        state.EntitySet.Remove(item);
        await Store.Commit(state);
    }

    public async Task Delete(IEnumerable<T> items)
    {
        var state = await Store.Transact();
        foreach (var entity in items)
        {
            state.EntitySet.Remove(entity);
        }
        await Store.Commit(state);
    }

    public virtual async Task Update(T items)
    {
        var state = await Store.Transact();

        var index = state.EntitySet.IndexOf(items);
        state.EntitySet.Remove(items);
        state.EntitySet.Insert(index, items);

        await Store.Commit(state);
    }
}
