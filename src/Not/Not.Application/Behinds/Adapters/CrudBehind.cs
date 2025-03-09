using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.CRUD.Lists.Ports;
using Not.Domain.Base;
using Not.Safe;

namespace Not.Application.Behinds.Adapters;

public abstract class CrudBehind<T, TModel>
    : ObservableListBehind<T>,
        IListBehind<T>,
        IFormBehind<TModel>
    where T : AggregateRoot
{
    readonly IRepository<T> _repository;
    ICrudParent<T>? _parentContext;
    List<ICrudReflection<T>> _reflections;

    /// <summary>
    /// Instantiates a CRUD behind capable of handling child items using <seealso cref="ICrudParent{T}"/>
    /// </summary>
    /// <param name="repository">Items repository</param>
    /// <param name="parentContext">ParentContext defines the necessary operations in order to update item's parent when item changes</param>

    protected CrudBehind(IRepository<T> repository, IEnumerable<ICrudReflection<T>> reflections)
        : base([])
    {
        _repository = repository;
        _reflections = reflections.ToList();
    }

    protected abstract T CreateEntity(TModel model);
    protected abstract T UpdateEntity(TModel model);

    public IReadOnlyList<T> Items => ObservableList;

    /// <summary>
    /// Attach CRUD parent context to be updated with changes in the state of <typeparamref name="T"/>
    /// <br/> CRUD treats parents as permissinve - delete/updates are not validated by the parent
    /// </summary>
    /// <param name="parentBehind"></param>
    protected void AttachParent(ICrudParent<T> parentBehind)
    {
        _parentContext = parentBehind;
    }
    
    protected void UpdateReflections<TReflection>(Func<T, TReflection?> selector, TReflection reflection, Action<T> update)
        where TReflection : class
    {
        foreach (var item in Items.Where(x => selector(x)?.Equals(reflection) ?? false))
        {
            update(item);
        }
    }

    // TODO: reevaluate the usefullnes around the complexities of this method - return for example
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

    public async Task Update(TModel model)
    {
        var entity = UpdateEntity(model);
        await _repository.Update(entity);
        if (_parentContext != null)
        {
            await _parentContext.Update(entity);
        }
        _reflections.ForEach(x => x.Reflect(entity));

        ObservableList.AddOrReplace(entity);
    }

    public async Task Create(TModel model)
    {
        var entity = CreateEntity(model);
        await _repository.Create(entity);
        if (_parentContext != null)
        {
            await _parentContext.Add(entity);
        }

        ObservableList.AddOrReplace(entity);
    }

    public async Task Delete(T entity)
    {
        await SafeHelper.Run(() => SafeDelete(entity));
    }

    async Task SafeDelete(T entity)
    {
        await _repository.Delete(entity);
        if (_parentContext != null)
        {
            await _parentContext.Remove(entity);
        }

        ObservableList.Remove(entity);
    }
}
