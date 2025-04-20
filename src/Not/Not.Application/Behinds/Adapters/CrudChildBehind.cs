using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.CRUD.Lists.Ports;
using Not.Domain.Base;
using Not.Safe;

namespace Not.Application.Behinds.Adapters;

public abstract class CrudChildBehind<T, TModel> : ObservableBehind, IListBehind<T>, IFormBehind<TModel>
    where T : AggregateRoot
{
    readonly List<ICrudReflection<T>> _reflections;
    readonly ICrudeParent<T> _crudeContext;

    /// <summary>
    /// Attach CRUD parent context to be updated with changes in the state of <typeparamref name="T"/>
    /// <br/> CRUD treats parents as permissinve - delete/updates are not validated by the parent
    /// </summary>
    protected CrudChildBehind(IEnumerable<ICrudReflection<T>> reflections, ICrudeParent<T> crudeContext)
    {
        _reflections = reflections.ToList();
        _crudeContext = crudeContext;
        _crudeContext.Changed.Subscribe(EmitChange);
    }

    protected abstract T CreateEntity(TModel model);
    protected abstract T UpdateEntity(TModel model);

    public IReadOnlyList<T> Items => _crudeContext.Children;
    
    protected sealed override Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        return Task.FromResult(true);
    }

    protected async Task SafeDelete(T entity)
    {
        await _crudeContext.Delete(entity);
    }

    public async Task Update(TModel model)
    {
        var entity = UpdateEntity(model);
        await _crudeContext.Update(entity);
        _reflections.ForEach(x => x.Reflect(entity));
    }

    public async Task Create(TModel model)
    {
        var entity = CreateEntity(model);
        await _crudeContext.Create(entity);
    }
    
    public async Task Delete(T entity)
    {
        await SafeHelper.Run(() => SafeDelete(entity));
    }
}
