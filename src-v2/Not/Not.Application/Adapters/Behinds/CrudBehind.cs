using Not.Application.Contexts;
using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Domain;
using Not.Exceptions;
using Not.Reflection;
using Not.Safe;

namespace Not.Application.Adapters.Behinds;

public abstract class CrudBehind<T, TModel> : ObservableListBehind<T>, IListBehind<T>, IFormBehind<TModel>, IDisposable
    where T : DomainEntity
{
    readonly Guid _loadedSubscriptionId;
    readonly IParentContext<T>? _parentContext;
    readonly IRepository<T> _repository;

    /// <summary>
    /// Instantiates a CRUD behind capable of handling child items
    /// </summary>
    /// <param name="repository">Items repository</param>
    /// <param name="parentContext">ParentContext defines the necessary operations in order to update item's parent when item changes</param>
    protected CrudBehind(IRepository<T> repository, IParentContext<T> parentContext) : base(parentContext.Children)
    {
        _repository = repository;
        _parentContext = parentContext;
        _loadedSubscriptionId = ObservableList.ChangedEvent.Subscribe(EmitChange);
    }
    /// <summary>
    /// Instatiates a basic CRUD behind for a standalone or root-level entity
    /// </summary>
    /// <param name="repository">Entity's repository</param>
    protected CrudBehind(IRepository<T> repository) : base([])
    {
        _repository = repository;
        _loadedSubscriptionId = ObservableList.ChangedEvent.Subscribe(EmitChange);
    }

    public IReadOnlyList<T> Items => ObservableList;

    protected abstract T CreateEntity(TModel model);
    protected abstract T UpdateEntity(TModel model);

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

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        if (_parentContext != null)
        {
            // CompetitionBehind assumes it's invoked in a EnduranceEvent tree context. I.e. that EnduranceEvent is already
            // initialized and _parentContext.Entity is not null. If it can be invoked autonomously then it can be initialized using args
            if (!_parentContext.HasLoaded() && !arguments.Any())
            {
                var name = this.GetTypeName();
                throw GuardHelper.Exception(
                    $"{name} is used as standalone child behind. " +
                    $"I.e. it depends on parent context '{_parentContext.GetTypeName()}' which isn't loaded." +
                    $"Either use initialize the context preemptively or pass parentId to '{name}.{nameof(IObservableBehind.Initialize)}'");
            }
            if (arguments.Any())
            {
                var argument = arguments.First();
                if (argument is not int parentId)
                {
                    throw GuardHelper.Exception($"Invalid argument '{argument.GetTypeName()}'");
                }
                await _parentContext.Load(parentId);
            }
            //ObservableCollection.AddRange(_parentContext.GetChildren());
            return false; // Has to be false in order to be able to reintialize and update if any children are changed
        }
        else
        {
            var entities = await _repository.ReadAll();
            ObservableList.AddRange(entities);
            return entities.Any();
        }
    }

    public async Task<TModel> Update(TModel model)
    {
        var entity = UpdateEntity(model);
        await OnBeforeUpdate(entity);
        await _repository.Update(entity);
        ObservableList.AddOrReplace(entity);
        return model;
    }

    public async Task<TModel> Create(TModel model)
    {
        var entity = CreateEntity(model);
        await OnBeforeCreate(entity);
        await _repository.Create(entity);
        ObservableList.AddOrReplace(entity);
        return model;
    }

    async Task<T> SafeDelete(T entity)
    {
        await OnBeforeDelete(entity);
        await _repository.Delete(entity);
        ObservableList.Remove(entity);
        return entity;
    }

    #region Safe pattern

    public async Task<T> Delete(T entity)
    {
        return await SafeHelper.Run(() => SafeDelete(entity)) ?? entity;
    }

    #endregion

    public void Dispose()
    {
        _parentContext?.Children.ChangedEvent.Unsubscribe(_loadedSubscriptionId);
        GC.SuppressFinalize(this);
    }
}
