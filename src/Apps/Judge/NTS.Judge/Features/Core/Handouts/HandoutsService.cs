using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Async.Extensions;
using Not.Exceptions;
using Not.Observables.Structures;
using Not.Safe;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Features.Core.Handouts;

public class HandoutsService
    : NStatefulService<ObservableList<HandoutDocument>>,
        IHandoutsService,
        ICreateHandout,
        ICoreDependentObservables,
        INotificationHandler<PhaseCompleted>
{
    readonly SemaphoreSlim _semaphore = new(1);
    readonly INtsSocketContext _socketContext;
    readonly IRepository<Handout> _handoutRepository;
    readonly IRepository<Participation> _participations;
    readonly IRepository<Official> _officials;

    public HandoutsService(
        INtsSocketContext socketContext,
        IRepository<Handout> handouts,
        IRepository<Participation> participations,
        IRepository<Official> officials
    )
    {
        _socketContext = socketContext;
        _handoutRepository = handouts;
        _participations = participations;
        _officials = officials;
    }

    public IReadOnlyList<HandoutDocument> Documents => State;

    protected override async Task<bool> InitializeState()
    {
        if (!_socketContext.IsConnected)
        {
            return false;
        }
        var handouts = await _handoutRepository.ReadMany();
        var officials = await _officials.ReadMany();
        if (State.Count != 0)
        {
            return true;
        }
        var documents = handouts.Select(handout => new HandoutDocument(handout, _socketContext.Event, officials));
        State.Replace(documents);
        return true;
    }

    public async Task Delete(IEnumerable<HandoutDocument> documents)
    {
        await _semaphore.WaitAsync();

        var ids = documents.Select(x => x.Id);
        await _handoutRepository.Delete(x => ids.Contains(x.Id));
        State.RemoveRange(documents);

        _semaphore.Release();
    }

    public async Task Create(int number)
    {
        var participation = await _participations.Read(x => x.Combination.Number == number);
        GuardHelper.ThrowIfDefault(participation);

        await CreateDocument(participation);
    }

    public async Task<IEnumerable<Combination>> GetCombinations()
    {
        return await SafeHelper.Run(SafeGetCombinations) ?? [];
    }

    public async Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        await CreateDocument(notification.Participation);
    }

    async Task<IEnumerable<Combination>> SafeGetCombinations()
    {
        return await _participations.ReadMany().Select(x => x.Combination);
    }

    async Task CreateDocument(Participation participation)
    {
        var enduranceEvent = GuardHelper.ThrowIfDefault(_socketContext.Event);
        var officials = await _officials.ReadMany();
        var existingHandout = await _handoutRepository.Read(x => x.Participation.Id == participation.Id);

        var handout = new Handout(participation, existingHandout?.Id);
        var document = new HandoutDocument(handout, enduranceEvent, officials);

        await _semaphore.WaitAsync();
        try
        {
            await _handoutRepository.Delete(x => x.Participation.Id == participation.Id);
            var existingDocuments = State.Where(x => x.ParticipationId == participation.Id).ToList();
            if (existingDocuments.Count != 0)
            {
                State.RemoveRange(existingDocuments);
            }
            await _handoutRepository.Create(handout);
            State.AddOrReplace(document);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
