using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.CRUD.Lists.Ports;
using Not.Blazor.Ports;
using Not.Cache;
using Not.Domain.Base;
using Not.Exceptions;
using Not.Reflection;
using Not.Safe;

namespace Not.Application.Behinds.Adapters;

public abstract class CrudBehind<T, TModel>
    : ObservableListBehind<T>,
        IListBehind<T>,
        IFormBehind<TModel>
    where T : AggregateRoot
{
    readonly IParentContext<T>? _parentContext;
    readonly IRepository<T> _repository;
    readonly ICache<T>? _cache;

    /// <summary>
    /// Instantiates a CRUD behind capable of handling child items using <seealso cref="IParentContext{T}"/>
    /// </summary>
    /// <param name="repository">Items repository</param>
    /// <param name="parentContext">ParentContext defines the necessary operations in order to update item's parent when item changes</param>
    protected CrudBehind(IRepository<T> repository, IParentContext<T> parentContext)
        : base(parentContext.Children)
    {
        _repository = repository;
        _parentContext = parentContext;
    }

    /// <summary>
    /// Instatiates a basic CRUD behind for a standalone or root-level entity
    /// </summary>
    /// <param name="repository">Entity's repository</param
    protected CrudBehind(IRepository<T> repository, ICache<T>? cache = null)
        : base([])
    {
        _repository = repository;
        _cache = cache;
    }

    protected abstract T CreateEntity(TModel model);
    protected abstract T UpdateEntity(TModel model);

    public IReadOnlyList<T> Items => ObservableList;
    public Guid Id { get; } = Guid.NewGuid();

    protected virtual async Task OnBeforeCreate(T entity)
    {
        if (_parentContext != null)
        {
            _parentContext.Add(entity);
            await _parentContext.Persist();
        }
    }

    protected virtual async Task OnBeforeUpdate(T entity)
    {
        if (_parentContext != null)
        {
            _parentContext.Update(entity);
            await _parentContext.Persist();
        }
    }

    protected virtual async Task OnBeforeDelete(T entity)
    {
        if (_parentContext != null)
        {
            _parentContext.Remove(entity);
            await _parentContext.Persist();
        }
    }

    // TODO: refactor this initialization flow. Currently after Update it emmits change and then PerformInitialization is called again
    // which leads to ObservableList being overwritten. This is not intuitive and is a problem, because then ICache is used that cache
    // also has to be updated. Too many sources of sporradic truth. But it also has to run every time in order to reflect changes in children
    // See comment at the end of the method
    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        if (_parentContext != null)
        {
            // Since it's it's a child it asssumes that it's parent is initiazliaed already. I.e. that EnduranceEvent is already
            // initialized and _parentContext.Entity is not null for Competitions.
            // If it can be invoked autonomously then it can be initialized using args
            if (!_parentContext.HasLoaded() && !arguments.Any())
            {
                var name = this.GetTypeName();
                var message =
                    $"{name} is used as standalone child behind. "
                    + $"I.e. it depends on parent context '{_parentContext.GetTypeName()}' which isn't loaded."
                    + $"Either use initialize the context preemptively or pass parentId to '{name}.{nameof(IObservableBehind.Initialize)}'";
                throw GuardHelper.Exception(message);
            }
            if (arguments.Any())
            {
                var argument = arguments.First();
                if (argument is not int parentId)
                {
                    var message = $"Invalid argument '{argument.GetTypeName()}'";
                    throw GuardHelper.Exception(message);
                }
                await _parentContext.Load(parentId);
            }
        }
        else
        {
            var entities = _cache != null
                ? await _cache.List()
                : await _repository.ReadAll();
            ObservableList.Clear();
            ObservableList.AddRange(entities);
        }
        // Has to be false in order to be able to reintialize and update if any children are changed
        return ActualizeEveryTime;
    }

    public async Task Update(TModel model)
    {
        var entity = UpdateEntity(model);
        await OnBeforeUpdate(entity);
        await _repository.Update(entity);
        ObservableList.AddOrReplace(entity);
        _cache?.Update(entity);
    }

    public async Task Create(TModel model)
    {
        var entity = CreateEntity(model);
        await OnBeforeCreate(entity);
        await _repository.Create(entity);
        ObservableList.AddOrReplace(entity);
        _cache?.Add(entity);
    }

    public async Task Delete(T entity)
    {
        await SafeHelper.Run(() => SafeDelete(entity));
    }

    async Task SafeDelete(T entity)
    {
        await OnBeforeDelete(entity);
        await _repository.Delete(entity);
        ObservableList.Remove(entity);
        _cache?.Delete(entity);
    }
}
