using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Blazor.CRUD.Forms.Ports;
using Not.Blazor.CRUD.Lists.Ports;
using Not.Domain.Aggregates;
using Not.Observables.Structures;

namespace Not.Application.Krud.Services;

public abstract class KrudRootService<T, TModel> : NStatefulService<ObservableList<T>>, IListBehind<T>, IFormBehind<TModel>
    where T : AggregateRoot
{
    readonly IRepository<T> _repository;
    readonly IEnumerable<IKrudMirror<T>> _mirrors;

    protected KrudRootService(IRepository<T> repository, IEnumerable<IKrudMirror<T>> mirrors) : base([])
    {
        _repository = repository;
        _mirrors = mirrors;
    }

    // TODO: replace with factory functions
    protected abstract T CreateAggregate(TModel model);
    protected abstract T UpdateAggregate(TModel model);

    public IReadOnlyList<T> Items => State;

    protected override async Task<bool> CreateState(params IEnumerable<object> arguments)
    {
        if (State.Any())
        {
            return false;
        }
        var entities = await _repository.ReadAll();
        State.AddRange(entities);
        return entities.Any();
    }

    public virtual async Task Update(TModel model)
    {
        var aggregate = UpdateAggregate(model);
        await _repository.Update(aggregate);
        State.AddOrReplace(aggregate);
        foreach (var mirror in _mirrors)
        {
            await mirror.Reflect(aggregate);
        }
    }

    public virtual async Task Create(TModel model)
    {
        var aggregate = CreateAggregate(model);
        await _repository.Create(aggregate);
        State.AddOrReplace(aggregate);
    }

    public virtual async Task Delete(T entity)
    {
        await _repository.Delete(entity);
        State.Remove(entity);
    }
}
