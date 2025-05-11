using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.CRUD.Lists.Ports;
using Not.Domain;
using Not.Domain.Base;
using Not.Exceptions;

namespace Not.Application.Behinds.Adapters;

public abstract class CrudBehind<T, TModel> : ObservableListBehind<T>, IListBehind<T>, IFormBehind<TModel>
    where T : AggregateRoot
{
    readonly IRepository<T> _repository;
    readonly List<ICrudReflection<T>> _reflections;

    protected CrudBehind(IRepository<T> repository, IEnumerable<ICrudReflection<T>> reflections)
    {
        _repository = repository;
        _reflections = reflections.ToList();
    }

    protected abstract T CreateEntity(TModel model);
    protected abstract T UpdateEntity(TModel model);

    public IReadOnlyList<T> Items => ObservableList;

    protected async Task Update<TUpdate>(Func<T, bool> filter, TUpdate update)
    {
        var matches = Items.Where(filter).ToList();
        if (matches is not IEnumerable<IReflect<TUpdate>> reflections)
        {
            throw GuardHelper.Exception(
                $"Invalid update '{typeof(TUpdate).Name}'. Type '{typeof(T).Name}' does not implement 'IReflect<TUpdate>'"
            );
        }
        foreach (var item in reflections)
        {
            item.Reflect(update);
            await _repository.Update((T)item);
        }
    }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        if (ObservableList.Any())
        {
            return Initialized;
        }
        var entities = await _repository.ReadAll();
        ObservableList.AddRange(entities);
        return entities.Any();
    }

    public virtual async Task Update(TModel model)
    {
        var entity = UpdateEntity(model);
        await _repository.Update(entity);
        ObservableList.AddOrReplace(entity);
        _reflections.ForEach(x => x.Reflect(entity));
    }

    public virtual async Task Create(TModel model)
    {
        var entity = CreateEntity(model);
        await _repository.Create(entity);
        ObservableList.AddOrReplace(entity);
    }

    public virtual async Task Delete(T entity)
    {
        await _repository.Delete(entity);
        ObservableList.Remove(entity);
    }
}
