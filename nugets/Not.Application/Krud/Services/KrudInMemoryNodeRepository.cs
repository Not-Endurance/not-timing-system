using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Domain.Aggregates;

namespace Not.Application.Krud.Services;

public class KrudInMemoryNodeRepository<T> : IRepository<T>
    where T : AggregateRoot
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
        var result = _parentNode.Children.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(result);
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
        var child = _parentNode.Children.FirstOrDefault(x => x.Id == id);
        if (child != null)
        {
            _parentNode.Remove(child);
        }
        return Task.CompletedTask;
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
        var ids = items.Select(x => x.Id).ToList();
        return Delete(x => ids.Contains(x.Id));
    }
}
