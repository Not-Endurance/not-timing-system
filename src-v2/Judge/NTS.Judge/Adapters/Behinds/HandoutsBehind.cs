using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Collections;
using Not.Concurrency;
using Not.Events;
using Not.Exceptions;
using Not.Safe;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Entities;
using NTS.Domain.Core.Events.Participations;
using NTS.Domain.Core.Objects;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Adapters.Behinds;

public class HandoutsBehind : ObservableBehind, IHandoutsBehind
{
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly IRepository<Handout> _handoutRepository;
    private readonly IRepository<Participation> _participations;
    private readonly IRepository<Event> _events;
    private readonly IRepository<Official> _officials;
    private ConcurrentList<HandoutDocument> _documents = [];

    public HandoutsBehind(
        IRepository<Handout> handouts,
        IRepository<Participation> participations,
        IRepository<Event> events,
        IRepository<Official> officials)
    {
        _handoutRepository = handouts;
        _participations = participations;
        _events = events;
        _officials = officials;
    }

    public IReadOnlyList<HandoutDocument> Documents => _documents.AsReadOnly();

    public void RunAtStartup()
    {
        // TODO: subscribe to updates for Event, Official
        EventHelper.Subscribe<PhaseCompleted>(PhaseCompletedHandler);
    }

    protected override async Task<bool> PerformInitialization()
    {
        var handouts = await _handoutRepository.ReadAll();

        //TODO: REMOVE
        if (!handouts.Any())
        {
            var allParticipations = await _participations.ReadAll();
            var list = new List<Handout>();
            foreach (var participation in allParticipations)
            {
                list.Add(new Handout(participation));
            }
            handouts = list;
        }

        var enduranceEvent = await _events.Read(0);
        var officials = await _officials.ReadAll();
        GuardHelper.ThrowIfDefault(enduranceEvent);

        var documents = handouts.Select(handout => new HandoutDocument(handout, enduranceEvent, officials));
        _documents = new(documents);
        
        EmitChange();
        return true;
    }

    async Task SafeDelete(IEnumerable<HandoutDocument> documents)
    {
        await _semaphore.WaitAsync();

        var ids = documents.Select(x => x.HandoutId);
        await _handoutRepository.Delete(x => ids.Contains(x.Id));
        _documents.RemoveRange(documents);

        _semaphore.Release();
    }

    async void PhaseCompletedHandler(PhaseCompleted phaseCompleted)
    {
        var @event = await _events.Read(0);
        var officials = await _officials.ReadAll();
        GuardHelper.ThrowIfDefault(@event);

        await _semaphore.WaitAsync();

        await HandleExistingHandout(phaseCompleted);

        var handout = new Handout(phaseCompleted.Participation);
        await _handoutRepository.Create(handout);

        var document = new HandoutDocument(handout, @event, officials);
        _documents.Add(document);

        _semaphore.Release();

        EmitChange();
    }

    async Task HandleExistingHandout(PhaseCompleted phaseCompleted)
    {
        var existing = await _handoutRepository.Read(x => x.Participation == phaseCompleted.Participation);
        if (existing != null)
        {
            await _handoutRepository.Delete(existing);
            //TODO: is this extension necessary :?
            _documents.RemoveIfExisting(x => x.Tandem.Number == phaseCompleted.Participation.Tandem.Number);
        }
    }

    #region SafePattern

    public async Task Delete(IEnumerable<HandoutDocument> documents)
    {
        await SafeHelper.Run(() => SafeDelete(documents));
    }

    #endregion
}
