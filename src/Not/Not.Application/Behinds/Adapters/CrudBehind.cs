using System.Transactions;
using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.CRUD.Lists.Ports;
using Not.Blazor.Ports;
using Not.Domain.Base;
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
    readonly IEnumerable<ISingleParentContext> _singleParentContexts;

    /// <summary>
    /// Instantiates a CRUD behind capable of handling child items using <seealso cref="IParentContext{T}"/>
    /// </summary>
    /// <param name="repository">Items repository</param>
    /// <param name="parentContext">ParentContext defines the necessary operations in order to update item's parent when item changes</param>
    protected CrudBehind(IRepository<T> repository, IEnumerable<ISingleParentContext> singleParentContexts, IParentContext<T> parentContext)
        : base(parentContext.Children)
    {
        _repository = repository;
        _singleParentContexts = singleParentContexts;
        _parentContext = parentContext;
    }

    /// <summary>
    /// Instatiates a basic CRUD behind for a standalone or root-level entity
    /// </summary>
    /// <param name="repository">Entity's repository</param
    protected CrudBehind(IRepository<T> repository, IEnumerable<ISingleParentContext> singleParentContexts)
        : base([])
    {
        _repository = repository;
        _singleParentContexts = singleParentContexts;
    }

    protected abstract T CreateEntity(TModel model);
    protected abstract T UpdateEntity(TModel model);

    public IReadOnlyList<T> Items => ObservableList;
    public Guid Id { get; } = Guid.NewGuid();

    protected virtual async Task OnBeforeCreate(T entity)
    {
        if (_parentContext != null)
        {
            await _parentContext.Add(entity);
        }
    }

    protected virtual async Task OnBeforeUpdate(T entity)
    {
        if (_parentContext != null)
        {
            await _parentContext.Update(entity);
        }
    }

    protected virtual async Task OnBeforeDelete(T entity)
    {
        if (_parentContext != null)
        {
            await _parentContext.Remove(entity);
        }
    }

    // TODO: reevaluate the usefullnes around the complexities of this method - return for example
    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        var entities = await _repository.ReadAll();
        ObservableList.AddRange(entities);
        return true;
    }

    public async Task Update(TModel model)
    {
        var entity = UpdateEntity(model);
        await OnBeforeUpdate(entity);
        await _repository.Update(entity);
        ObservableList.AddOrReplace(entity);
        foreach (var singleParentContext in _singleParentContexts)
        {
            singleParentContext.Update(entity);
        }
    }

    public async Task Create(TModel model)
    {
        var entity = CreateEntity(model);
        await OnBeforeCreate(entity);
        await _repository.Create(entity);
        ObservableList.AddOrReplace(entity);
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
    }
}
