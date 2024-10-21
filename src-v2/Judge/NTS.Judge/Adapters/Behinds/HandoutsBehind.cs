using Not.Application.Adapters.Behinds;
using Not.Application.Ports.CRUD;
using Not.Exceptions;
using Not.Safe;
using NTS.Domain.Core.Entities;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Blazor.Ports;

namespace NTS.Judge.Adapters.Behinds;

public class HandoutsBehind : ObservableListBehind<HandoutDocument>, IHandoutsBehind
{
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly IRepository<Handout> _handoutRepository;
    private readonly IRepository<Participation> _participations;
    private readonly IRepository<EnduranceEvent> _events;
    private readonly IRepository<Official> _officials;

    public HandoutsBehind(
        IRepository<Handout> handouts,
        IRepository<Participation> participations,
        IRepository<EnduranceEvent> events,
        IRepository<Official> officials)
    {
        _handoutRepository = handouts;
        _participations = participations;
        _events = events;
        _officials = officials;
    }

    public IReadOnlyList<HandoutDocument> Documents => ObservableList;

    public void RunAtStartup()
    {
        // TODO: subscribe to updates for Event, Official
        Participation.PhaseCompletedEvent.SubscribeAsync(PhaseCompletedHandler);
    }

    protected override async Task<bool> PerformInitialization(params IEnumerable<object> arguments)
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

        var documents = handouts.Select(handout => new HandoutDocument(handout.Participation, enduranceEvent, officials));
        ObservableList.AddRange(documents);
        
        return true;
    }

    async Task SafeDelete(IEnumerable<HandoutDocument> documents)
    {
        await _semaphore.WaitAsync();

        var ids = documents.Select(x => x.Id);
        await _handoutRepository.Delete(x => ids.Contains(x.Id));
        ObservableList.RemoveRange(documents); 

        _semaphore.Release();
    }

    async void PhaseCompletedHandler(PhaseCompleted phaseCompleted)
    {
        var enduranceEvent = await _events.Read(0);
        var officials = await _officials.ReadAll();
        GuardHelper.ThrowIfDefault(enduranceEvent);

        var handout = new Handout(phaseCompleted.Participation);
        var document = new HandoutDocument(phaseCompleted.Participation, enduranceEvent, officials);

        await _semaphore.WaitAsync();

        await _handoutRepository.Delete(x => x.Participation == phaseCompleted.Participation);
        await _handoutRepository.Create(handout); 
        ObservableList.AddOrReplace(document);

        _semaphore.Release();
    }

    #region SafePattern

    public async Task Delete(IEnumerable<HandoutDocument> documents)
    {
        await SafeHelper.Run(() => SafeDelete(documents));
    }

    #endregion
}
