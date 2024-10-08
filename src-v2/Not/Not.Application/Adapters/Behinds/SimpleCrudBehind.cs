using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Domain;
using Not.Safe;
using Not.Structures;

namespace Not.Application.Adapters.Behinds;

public abstract class SimpleCrudBehind<T, TModel> : ObservableBehind, IListBehind<T>, IFormBehind<TModel>
    where T : DomainEntity
{
    protected SimpleCrudBehind(IRepository<T> repository)
    {
        ObservableCollection = new(EmitChange);
        Repository = repository;
    }

    protected EntitySet<T> ObservableCollection { get; }
    protected IRepository<T> Repository { get; }

    public IReadOnlyList<T> Items => ObservableCollection;

    protected abstract T CreateEntity(TModel model);
    protected abstract T UpdateEntity(TModel model);

    protected virtual Task OnBeforeCreate(T entity)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnBeforeUpdate(T entity)
    {
        return Task.CompletedTask;
    }

    protected virtual Task OnBeforeDelete(T entity)
    {
        return Task.CompletedTask;
    }

    // TODO: probably remove SafeCreate and SafeUpdate as they interfere with Form validation mechanism
    // where errors are handled anyway. Use SafeHelper for form validation mechanism:
    // use optional parameter to override the default validation behavior
    protected virtual async Task<TModel> SafeCreate(TModel model)
    {
        var entity = CreateEntity(model);
        await OnBeforeCreate(entity);
        await Repository.Create(entity);
        ObservableCollection.AddOrReplace(entity);
        return model;
    }

    protected virtual async Task<TModel> SafeUpdate(TModel model)
    {
        var entity = UpdateEntity(model);
        await OnBeforeUpdate(entity);
        await Repository.Update(entity);
        ObservableCollection.AddOrReplace(entity);
        return model;
    }

    protected virtual async Task<T> SafeDelete(T entity)
    {
        await OnBeforeDelete(entity);
        await Repository.Delete(entity);
        ObservableCollection.Remove(entity);
        return entity;
    }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> _)
    {
        var entities = await Repository.ReadAll();
        ObservableCollection.AddRange(entities);
        return entities.Any();
    }

    public async Task<TModel> Create(TModel model)
    {
        return await SafeHelper.Run(() => SafeCreate(model)) ?? model;
    }

    public async Task<T> Delete(T entity)
    {
        return await SafeHelper.Run(() => SafeDelete(entity)) ?? entity;
    }

    public async Task<TModel> Update(TModel model)
    {
        return await SafeHelper.Run(() => SafeUpdate(model)) ?? model;
    }
}
