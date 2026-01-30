using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Domain;

namespace Not.Application.Krud.Services;

public class KrudInMemoryNodeRepository<T> : IRepository<T>
    where T : Entity
{
    readonly IKrudParentNodeOf<T> _parentNode;

    public KrudInMemoryNodeRepository(IKrudParentNodeOf<T> parentNode)
    {
        _parentNode = parentNode;
    }

    public Task Create(T item)
    {
        _parentNode.Add(item);
        return Task.CompletedTask;
    }

    public Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        var result = _parentNode.Children.FirstOrDefault(predicate);
        return Task.FromResult(result);
    }

    public Task<T?> Read(int id)
    {
        throw new NotImplementedException("Krud shouldn't need Read by ID");
    }

    public Task<IEnumerable<T>> ReadMany()
    {
        var result = _parentNode.Children.AsEnumerable();
        return Task.FromResult(result);
    }

    public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        var result = _parentNode.Children.Where(predicate);
        return Task.FromResult(result);
    }

    public Task Update(T child)
    {
        _parentNode.Update(child);
        return Task.CompletedTask;
    }

    public Task Delete(int id)
    {
        throw new NotImplementedException("Krud shouldn't need Delete by ID");
    }

    public Task Delete(T child)
    {
        _parentNode.Remove(child);
        return Task.CompletedTask;
    }

    public Task Delete(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        var children = _parentNode.Children.Where(predicate);
        foreach (var child in children)
        {
            _parentNode.Remove(child);
        }
        return Task.CompletedTask;
    }

    public Task Delete(IEnumerable<T> items)
    {
        return Delete(x => items.Any(y => x == y));
    }
}
