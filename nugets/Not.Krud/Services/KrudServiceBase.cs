using Not.Application.CRUD.Ports;
using Not.Application.Services;
using Not.Domain;
using Not.Domain.Exceptions;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Krud.Models;

namespace Not.Krud.Services;

public abstract class KrudServiceBase<T, TModel> : IKrudListBehind<T>, IKrudFormService<TModel>
    where T : Entity
    where TModel : IKrudModel<T>, IKrudFormModel, new()
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
            throw GuardHelper.Exception(
                $"'{nameof(Create)}' requires null value for Id, otherwise it might result in an '{nameof(Update)}'"
            );
        }
        var entity = MapEntity(model);
        await _repository.Create(entity);
    }

    public virtual async Task Update(TModel model)
    {
        GuardHelper.ThrowIfDefault(
            model.Id,
            $"'{nameof(Update)}' does not allow Id to be null as it might result in '{nameof(Create)}'"
        );
        var entity = MapEntity(model);
        await _repository.Update(entity);
        foreach (var mirror in _mirrors)
        {
            await mirror.Reflect(entity);
        }
    }

    public virtual async Task Delete(T entity)
    {
        var impact = await PreviewDelete(entity);
        if (impact.HasUsages)
        {
            throw new DomainException(
                $"'{impact.Target}' is used by {impact.Usages.Count} related item(s). Confirm cascading delete to continue."
            );
        }
        await _repository.Delete(entity);
    }

    public virtual async Task<KrudDeleteImpact> PreviewDelete(T entity)
    {
        if (_repository is not IKrudCascadeRepository<T> cascade)
        {
            return new KrudDeleteImpact(Label(entity), []);
        }
        return await cascade.PreviewDelete(entity);
    }

    public virtual async Task DeleteCascade(T entity)
    {
        if (_repository is IKrudCascadeRepository<T> cascade)
        {
            await cascade.DeleteCascade(entity);
            return;
        }
        await _repository.Delete(entity);
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
