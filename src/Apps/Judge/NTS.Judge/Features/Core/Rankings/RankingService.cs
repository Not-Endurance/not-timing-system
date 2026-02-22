using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Exceptions;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Notify;
using Not.Observables.Structures;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Features.Core.Rankings.CustomRankings;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Features.Core.Rankings;

public class RankingService
    : NStatefulService<ObservableList<Ranking>>,
        IKrudFormService<CustomRankingModel>,
        IRankingService,
        IRankingMenuService,
        IRanklistDocumentFactory,
        ICustomRankingService,
        ICoreDependentObservables,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<ParticipationRestored>,
        ISingleton
{
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<EnduranceEvent> _events;
    readonly IRepository<Official> _officials;
    readonly IRepository<ArchiveEntry> _archive;
    readonly INotifier _notifier;
    Ranking? _current;

    public RankingService(
        IRepository<Ranking> rankings,
        IRepository<EnduranceEvent> events,
        IRepository<Official> officials,
        IRepository<ArchiveEntry> archive,
        INotifier notifier
    )
    {
        _rankings = rankings;
        _events = events;
        _officials = officials;
        _archive = archive;
        _notifier = notifier;
    }

    public Ranking Current =>
        GuardHelper.ThrowIfDefault(
            _current,
            $"'{nameof(RankingService)}.{nameof(Current)}' shouldn't be used before '{InitializeState}' has completed. Did you forget to call 'Observe'?"
        );

    public ObservableList<Ranking> Rankings => State;

    protected override async Task<bool> InitializeState()
    {
        var rankings = await _rankings.ReadMany();
        if (!rankings.Any())
        {
            return false;
        }
        _current = rankings.First();
        Rankings.Replace(rankings);
        return true;
    }

    public async Task Create(CustomRankingModel model)
    {
        var ranking = model.MapToEntity();
        await _rankings.Create(ranking);
        Rankings.AddOrReplace(ranking);
    }

    public Task Update(CustomRankingModel item)
    {
        throw new NotImplementedException();
    }

    public async Task Delete(Ranking ranking)
    {
        await _rankings.Delete(ranking);
        Rankings.Remove(ranking);
    }

    public async Task ArchiveEnduranceEvent()
    {
        var enduranceEvent = await _events.Read(0);
        if (enduranceEvent == null)
        {
            _notifier.Warn("Event is not started yet");
            return;
        }
        var officials = await _officials.ReadMany();
        var rankings = await _rankings.ReadMany();
        var ranklists = rankings.Select(x => new Ranklist(x)).Where(x => x.Entries.Any());

        var entry = new ArchiveEntry(enduranceEvent, officials, ranklists);
        await _archive.Create(entry);
    }

    public async Task<RanklistDocument> Create(Ranking ranking)
    {
        var enduranceEvent = await _events.Read(0);
        var officials = await _officials.ReadMany();
        var ranklist = new Ranklist(ranking);
        return new RanklistDocument(ranklist, enduranceEvent!, officials);
    }

    public void Select(Ranking ranking)
    {
        _current = ranking;
        EmitChanged();
    }

    public Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        UpdateRanklist(notification);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationEliminated notification, CancellationToken cancellationToken)
    {
        UpdateRanklist(notification);
        return Task.CompletedTask;
    }

    public Task Handle(ParticipationRestored notification, CancellationToken cancellationToken)
    {
        UpdateRanklist(notification);
        return Task.CompletedTask;
    }

    void UpdateRanklist(ParticipationPayload payload)
    {
        if (_current == null)
        {
            return;
        }
        foreach (var ranking in Rankings)
        {
            ranking.Update(payload.Participation);
        }
        EmitChanged();
    }
}
