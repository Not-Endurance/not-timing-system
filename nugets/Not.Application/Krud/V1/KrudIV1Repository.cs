using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain.Aggregates;
using Not.Exceptions;

namespace Not.Application.Krud.V1;

public abstract class KrudIV1Repository<T> : IRepository<T>
    where T : AggregateRoot
{
    readonly KrudParentNodeOf<T> _parentNode;

    protected KrudIV1Repository(KrudParentNodeOf<T> parentNode)
    {
        _parentNode = parentNode;
    }

    public Task Create(T item)
    {
        GuardHelper.ThrowIfDefault(_parentNode.Aggregate);
        _parentNode.Aggregate.Add(item);
        return Task.CompletedTask;
    }

    public Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        GuardHelper.ThrowIfDefault(_parentNode.Aggregate);
        var predicate = filter.Compile();
        var result = _parentNode.Aggregate.Children.FirstOrDefault(predicate);
        return Task.FromResult(result);
    }

    public Task<T?> Read(int id)
    {
        GuardHelper.ThrowIfDefault(_parentNode.Aggregate);
        var result = _parentNode.Aggregate.Children.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(result);
    }

    public Task<IEnumerable<T>> ReadMany()
    {
        GuardHelper.ThrowIfDefault(_parentNode.Aggregate);
        var result = _parentNode.Aggregate.Children.AsEnumerable();
        return Task.FromResult(result);
    }

    public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        GuardHelper.ThrowIfDefault(_parentNode.Aggregate);

        var predicate = filter.Compile();
        var result = _parentNode.Aggregate.Children.Where(predicate);
        return Task.FromResult(result);
    }

    public Task Update(T child)
    {
        GuardHelper.ThrowIfDefault(_parentNode.Aggregate);
        _parentNode.Aggregate.Update(child);
        return Task.CompletedTask;
    }

    public Task Delete(int id)
    {
        GuardHelper.ThrowIfDefault(_parentNode.Aggregate);
        var child = _parentNode.Aggregate.Children.FirstOrDefault(x => x.Id == id)
            ?? throw GuardHelper.Exception($"{typeof(T).Name} with '{id}' not found");
        _parentNode.Aggregate.Remove(child);
        return Task.CompletedTask;
    }

    public Task Delete(T child)
    {
        GuardHelper.ThrowIfDefault(_parentNode.Aggregate);
        _parentNode.Aggregate.Remove(child);
        return Task.CompletedTask;
    }

    public Task Delete(Expression<Func<T, bool>> filter)
    {
        GuardHelper.ThrowIfDefault(_parentNode.Aggregate);
        var predicate = filter.Compile();
        var children = _parentNode.Aggregate.Children.Where(predicate);
        foreach (var child in children)
        {
            _parentNode.Aggregate.Remove(child);
        }
        return Task.CompletedTask;
    }

    public Task Delete(IEnumerable<T> items)
    {
        var ids = items.Select(x => x.Id).ToList();
        return Delete(x => ids.Contains(x.Id));
    }
}
