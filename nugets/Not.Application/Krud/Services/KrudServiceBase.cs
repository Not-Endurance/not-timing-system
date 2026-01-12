using Not.Application.CRUD.Ports;
using Not.Application.Krud.Abstractions;
using Not.Application.Services;
using Not.Domain.Aggregates;

namespace Not.Application.Krud.Services;

public abstract class KrudServiceBase<T, TModel> : IListBehind<T>, IFormBehind<TModel>
    where T : AggregateRoot
{
    readonly List<IKrudMirror<T>> _mirrors;
    private readonly IRepository<T> _repository;

    protected KrudServiceBase(IRepository<T> repository, IEnumerable<IKrudMirror<T>> reflections)
    {
        _repository = repository;
        _mirrors = reflections.ToList();
    }

    protected abstract T CreateEntity(TModel model);
    protected virtual T UpdateEntity(TModel model)
    {
        return CreateEntity(model);
    }

    public virtual async Task<IEnumerable<T>> ReadMany()
    {
        return await _repository.ReadMany();
    }

    public virtual async Task Create(TModel model)
    {
        var entity = CreateEntity(model);
        await _repository.Create(entity);
    }

    public virtual async Task Update(TModel model)
    {
        var entity = UpdateEntity(model);
        await _repository.Update(entity);
        foreach (var mirror in _mirrors)
        {
            await mirror.Reflect(entity);
        }
    }

    public virtual async Task Delete(T entity)
    {
        await _repository.Delete(entity);
    }
}
