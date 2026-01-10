using Not.Application.Behinds.Adapters;
using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.CRUD.Lists.Ports;
using Not.Domain.Aggregates;
using Not.Safe;

namespace Not.Application.Krud.Services;

public abstract class KrudService<T, TModel> : NStatefulService, IListBehind<T>, IFormBehind<TModel>
    where T : AggregateRoot
{
    readonly List<IKrudMirror<T>> _reflections;
    readonly IKrudParentNodeOf<T> _parentNode;

    /// <summary>
    /// Attach CRUD parent context to be updated with changes in the state of <typeparamref name="T"/>
    /// <br/> CRUD treats parents as permissinve - delete/updates are not validated by the parent
    /// </summary>
    protected KrudService(IEnumerable<IKrudMirror<T>> reflections, IKrudParentNodeOf<T> parentNode)
    {
        _reflections = reflections.ToList();
        _parentNode = parentNode;
        _parentNode.Changed.Subscribe(EmitChanged);
    }

    protected abstract T CreateEntity(TModel model);

    public IReadOnlyList<T> Items => _parentNode.Children;

    protected virtual T UpdateEntity(TModel model)
    {
        return CreateEntity(model);
    }

    protected sealed override Task<bool> CreateState(params IEnumerable<object> arguments)
    {
        return Task.FromResult(true);
    }

    protected async Task SafeDelete(T entity)
    {
        await _parentNode.Delete(entity);
    }

    public async Task Update(TModel model)
    {
        var entity = UpdateEntity(model);
        await _parentNode.Update(entity);
        _reflections.ForEach(x => x.Reflect(entity));
    }

    public async Task Create(TModel model)
    {
        var entity = CreateEntity(model);
        await _parentNode.Create(entity);
    }

    public async Task Delete(T entity)
    {
        await SafeHelper.Run(() => SafeDelete(entity));
    }
}
