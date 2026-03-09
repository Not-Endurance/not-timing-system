using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain;
using Not.Krud.Abstractions;
using Not.Krud.Models;

namespace Not.Krud.Services;

public class KrudInMemoryNodeRepository<T> : IRepository<T>, IKrudCascadeRepository<T>
    where T : Entity
{
    readonly IKrudParentNodeOf<T> _parentNode;
    readonly IKrudDependencyResolver? _dependencyResolver;

    public KrudInMemoryNodeRepository(IKrudParentNodeOf<T> parentNode, IEnumerable<IKrudDependencyResolver> resolvers)
    {
        _parentNode = parentNode;
        _dependencyResolver = resolvers.FirstOrDefault(x => x.Supports(typeof(T)));
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
        var children = _parentNode.Children.Where(predicate).ToList();
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

    public Task<KrudDeleteImpact> PreviewDelete(T entity)
    {
        if (_dependencyResolver == null)
        {
            return Task.FromResult(new KrudDeleteImpact(Label(entity), []));
        }
        return Task.FromResult(_dependencyResolver.PreviewDelete(entity));
    }

    public Task DeleteCascade(T entity)
    {
        _dependencyResolver?.CascadeDeleteDependencies(entity);
        _parentNode.Remove(entity);
        return Task.CompletedTask;
    }

    static string Label(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }
        try
        {
            var text = value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }
        catch { }
        return value.GetType().Name;
    }
}
