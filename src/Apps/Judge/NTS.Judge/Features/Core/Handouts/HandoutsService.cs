using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Async.Extensions;
using Not.Exceptions;
using Not.Observables.Structures;
using Not.Safe;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Features.Core.Handouts;

public class HandoutsService
    : NStatefulService<ObservableList<ResultsDocument>>,
        IHandoutsService,
        ICreateHandout,
        ICoreDependentObservables,
        INotificationHandler<PhaseCompleted>
{
    readonly SemaphoreSlim _semaphore = new(1);
    readonly INtsSocketContext _socketContext;
    readonly IEventScopedRepository<Handout> _handoutRepository;
    readonly IEventScopedRepository<Participation> _participations;
    readonly IEventScopedRepository<Official> _officials;

    public HandoutsService(
        INtsSocketContext socketContext,
        IEventScopedRepository<Handout> handouts,
        IEventScopedRepository<Participation> participations,
        IEventScopedRepository<Official> officials
    )
    {
        _socketContext = socketContext;
        _handoutRepository = handouts;
        _participations = participations;
        _officials = officials;
    }

    public IReadOnlyList<ResultsDocument> Documents => State;

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
        var documents = handouts.Select(handout => new ResultsDocument(handout, _socketContext.Event, officials));
        State.Replace(documents);
        return true;
    }

    public async Task Delete(IEnumerable<ResultsDocument> documents)
    {
        var documentList = documents.ToList();
        var ids = documentList.Select(x => x.Id).ToHashSet();

        await _semaphore.WaitAsync();
        try
        {
            await _handoutRepository.DeleteMany(x => ids.Contains(x.Id));
            State.RemoveRange(documentList);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task Create(int number)
    {
        var participation = await _participations.Read(x => x.Combination.Number == number);
        GuardHelper.ThrowIfDefault(participation);

        await CreateDocument(participation);
    }

    public async Task<IEnumerable<Combination>> GetCombinations()
    {
        return await SafeHelper.RunWithError(SafeGetCombinations);
    }

    public async Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        if (notification.Participation.Phases.Current.IsFinal)
        {
            return;
        }

        await CreateDocument(notification.Participation);
    }

    async Task<IEnumerable<Combination>> SafeGetCombinations()
    {
        return await _participations.ReadMany().Select(x => x.Combination);
    }

    async Task CreateDocument(Participation participation)
    {
        var eventInformation = GuardHelper.ThrowIfDefault(_socketContext.Event);
        var officials = await _officials.ReadMany();

        var handout = new Handout(participation);
        var document = new ResultsDocument(handout, eventInformation, officials);

        await _semaphore.WaitAsync();
        try
        {
            await _handoutRepository.DeleteMany(x => x.Entries.Any(entry => entry.ParticipationId == participation.Id));
            var existingDocuments = State
                .Where(x => x.Entries.Any(entry => entry.ParticipationId == participation.Id))
                .ToList();
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
