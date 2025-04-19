using Not.Application.CRUD.Ports;
using Not.Domain.Base;
using Not.Structures;

namespace Not.Application.Behinds.Adapters;

public abstract class CrudChildBehind<T, TModel> : CrudBehind<T, TModel>
    where T : AggregateRoot
{
    readonly IRepository<T> _repository;
    readonly List<ICrudReflection<T>> _reflections;
    readonly ICrudParent<T> _crudeContext;

    /// <summary>
    /// Attach CRUD parent context to be updated with changes in the state of <typeparamref name="T"/>
    /// <br/> CRUD treats parents as permissinve - delete/updates are not validated by the parent
    /// </summary>
    protected CrudChildBehind(
        IRepository<T> repository,
        IEnumerable<ICrudReflection<T>> reflections,
        ICrudParent<T> crudeContext
    )
        : base(repository, reflections)
    {
        _repository = repository;
        _reflections = reflections.ToList();
        _crudeContext = crudeContext;
        ObservableList = crudeContext.Children;
    }

    protected sealed override ObservableList<T> ObservableList => _crudeContext.Children;

    protected sealed override Task<bool> PerformInitialization(params IEnumerable<object> arguments)
    {
        return Task.FromResult(true);
    }

    protected override async Task SafeDelete(T entity)
    {
        await _crudeContext.Remove(entity);
        await _repository.Delete(entity);
        ObservableList.Remove(entity);
    }

    public override async Task Update(TModel model)
    {
        var entity = UpdateEntity(model);
        await _crudeContext.Update(entity);
        await _repository.Update(entity);
        ObservableList.AddOrReplace(entity);
        _reflections.ForEach(x => x.Reflect(entity));
    }

    public override async Task Create(TModel model)
    {
        var entity = CreateEntity(model);
        await _crudeContext.Add(entity);
        await _repository.Create(entity);
        ObservableList.AddOrReplace(entity);
    }
}
