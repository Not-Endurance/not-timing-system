using Not.Application.CRUD.Ports;
using Not.Application.Services;
using Not.Domain;
using Not.Exceptions;
using Not.Krud.Abstractions;

namespace Not.Krud.Services;

public abstract class KrudServiceBase<T, TModel> : IListBehind<T>, IKrudFormService<TModel>
    where T : Entity
    where TModel: IKrudModel<T>, IKrudFormModel, new()
{
    readonly List<IKrudMirror<T>> _mirrors;
    readonly IRepository<T> _repository;

    protected KrudServiceBase(IRepository<T> repository, IEnumerable<IKrudMirror<T>> reflections)
    {
        _repository = repository;
        _mirrors = reflections.ToList();
    }

    protected virtual T MapEntity(TModel model)
    {
        return model.MapToEntity();
    }

    public virtual async Task<IEnumerable<T>> ReadMany()
    {
        return await _repository.ReadMany();
    }

    public virtual async Task Create(TModel model)
    {
        if (model.Id != null)
        {
            throw GuardHelper.Exception($"'{nameof(Create)}' requires null value for Id, otherwise it might result in an '{nameof(Update)}'");
        }
        var entity = MapEntity(model);
        await _repository.Create(entity);
    }

    public virtual async Task Update(TModel model)
    {
        GuardHelper.ThrowIfDefault(
            model.Id,
            $"'{nameof(Update)}' does not allow Id to be null as it might result in '{nameof(Create)}'");
        var entity = MapEntity(model);
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
