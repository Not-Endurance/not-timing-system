using Not.Application.CRUD.Ports;
using Not.Application.Krud.Nodes;
using Not.Domain.Aggregates;
using Not.Exceptions;
using Not.Observables;
using Not.Startup;

namespace Not.Application.Krud.Services;

public class KrudGraphContext<T> : Observer, IStartupInitializer
    where T : AggregateRoot
{
    readonly KrudNode _root;
    readonly IRepository<T> _repository;

    public KrudGraphContext(IRepository<T> repository, KrudRootProvider<T> provider)
    {
        _repository = repository;

        var message = $" is null. This is a bug - '{nameof(KrudRootProvider<>.SetRootNode)}' must be called during service registration";
        GuardHelper.ThrowIfDefault(provider?.Root, message);
        _root = provider.Root;
    }

    public void RunAtStartup()
    {
        Observe(_root, CommitCoalesced);
    }

    async Task CommitCoalesced()
    {
        GuardHelper.ThrowIfDefault(_root?.Value);
        await _repository.Update((T)_root.Value); // TODO add coalescing logic
    }
}
