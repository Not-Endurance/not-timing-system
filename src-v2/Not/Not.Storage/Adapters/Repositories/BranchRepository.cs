using Not.Storage.Ports.States;

namespace Not.Storage.Adapters.Repositories;

/// <summary>
/// Represent a set of operations for non-root level entiries in a Tree-like data structure. 
/// That includes all entities in the structure except the root, regardless of being parents or not
/// Implements IReposistory to allow for streamline API, but does not support any Create or Delete methods.
/// Instead you should use <seealso cref="IParent{T}"/> operations and Update the parent itself
/// </summary>
/// <typeparam name="T">Type of the Root entity</typeparam>
/// <typeparam name="TState">Type of the state object containing the Root entity</typeparam>
public abstract class BranchRepository<T, TState> : IRepository<T>
    where T : DomainEntity
    where TState : class, IState, new()
{
    private readonly IStore<TState> _store;

    protected BranchRepository(IStore<TState> store)
    {
        _store = store;
    }

    protected abstract IParent<T>? GetParent(TState state, int childId);
    protected abstract T? Get(TState state, int id);

    public virtual async Task<T?> Read(int id)
    {
        var state = await _store.Readonly();
        return Get(state, id);
    }

    public async Task<T> Update(T entity)
    {
        var state = await _store.Transact();
        var parent = GetParent(state, entity.Id);
        GuardHelper.ThrowIfDefault(parent);

        parent.Update(entity);
        await _store.Commit(state);

        return entity;
    }

    public Task<T?> Read(Predicate<T> filter)
    {
        throw NotImplemented();
    }
    public Task<IEnumerable<T>> ReadAll()
    {
        throw NotImplemented();
    }
    public virtual Task<IEnumerable<T>> ReadAll(Predicate<T> filter)
    {
        throw NotImplemented();
    }

    public Task<T> Create(T entity)
    {
        throw NotImplemented();
    }
    public Task<T> Delete(int id)
    {
        throw NotImplemented();
    }
    public Task<T> Delete(Predicate<T> filter)
    {
        throw NotImplemented();
    }
    public Task<T> Delete(T entity)
    {
        throw NotImplemented();
    }

    private Exception NotImplemented()
    {
        return new NotImplementedException($"Only 'Read' and 'Update' operations are implemented on '{nameof(BranchRepository<T, TState>)}'");
    }
}
