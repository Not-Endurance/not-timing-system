using Not.Domain.Exceptions;
using Not.Events;
using NTS.Contracts.Core;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects.Documents;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// A Live Event as a page shows it, over a store of its Participations and without a connection (#646): what a service of the
/// Console asks of the view it was opened with, so that the service is tried over the Api in this process and the hub is left
/// to the tests of the hub. It may write, or it may not, and then it refuses as a view of a Historic Event does.
/// </summary>
internal sealed class StubViewedEvent : IViewedEvent
{
    readonly IParticipationStore _store;
    readonly Event _changed = new();

    public StubViewedEvent(IParticipationStore store, EventInformation @event, bool canWrite = true)
    {
        _store = store;
        Event = @event;
        CanWrite = canWrite;
    }

    public IEventSubscriber ObservableEvent => _changed;
    public EventInformation Event { get; }
    public EventStage Stage => EventStage.Live;
    public bool IsLive => true;
    public bool CanWrite { get; }
    public IReadOnlyList<Participation> Participations => _store.Participations;
    public IReadOnlyList<Ranking> Rankings => [];
    public IReadOnlyList<Official> Officials => [];

    public Task Load()
    {
        return Task.CompletedTask;
    }

    public Participation? Find(Guid id)
    {
        return _store.Find(id);
    }

    public ResultsDocument CreateDocument(Ranking ranking)
    {
        throw new NotSupportedException("The stub of a view composes no Results.");
    }

    public bool Shows(CoreView view)
    {
        return true;
    }

    public void EnsureCanWrite()
    {
        if (!CanWrite)
        {
            throw new DomainException("This view cannot write.");
        }
    }

    public void Dispose() { }
}
