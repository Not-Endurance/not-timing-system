using MediatR;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Exceptions;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Observables.Structures;
using NTS.Application.Socket;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects;
using NTS.Domain.Core.Objects.Documents;
using NTS.Domain.Core.Objects.Payloads;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Features.Core.Rankings;

public class RankingService
    : NStatefulService<ObservableList<Ranking>>,
        IKrudFormService<CustomRankingModel>,
        IRankingService,
        IRankingMenuService,
        IRanklistDocumentService,
        ICoreDependentObservables,
        INotificationHandler<PhaseCompleted>,
        INotificationHandler<ParticipationEliminated>,
        INotificationHandler<ParticipationRestored>,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly INtsSocketContext _socketContext;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<Official> _officials;
    readonly IRepository<ArchiveEntry> _archive;
    readonly CoalesceInvoker _coalesced;
    Ranking? _current;
    IReadOnlyList<Official> _loadedOfficials = [];

    public RankingService(
        INtsSocketContext socketContext,
        IRepository<Ranking> rankings,
        IRepository<Official> officials,
        IRepository<ArchiveEntry> archive
    )
    {
        _socketContext = socketContext;
        _rankings = rankings;
        _officials = officials;
        _archive = archive;
        _coalesced = new();
    }

    public Ranking Current =>
        GuardHelper.ThrowIfDefault(
            _current ?? Rankings.FirstOrDefault(),
            $"'{nameof(RankingService)}.{nameof(Current)}' shouldn't be used before '{InitializeState}' has completed. Did you forget to call 'Observe'?"
        );

    public ObservableList<Ranking> Rankings => State;

    protected override async Task<bool> InitializeState()
    {
        if (!_socketContext.IsConnected)
        {
            Rankings.Clear();
            _current = null;
            _loadedOfficials = [];
            return false;
        }

        _loadedOfficials = (await _officials.ReadMany()).ToList();

        var rankings = (await _rankings.ReadMany()).ToList();
        if (!rankings.Any())
        {
            Rankings.Clear();
            _current = null;
            return false;
        }

        var currentId = _current?.Id;
        Rankings.Replace(rankings);
        _current = rankings.FirstOrDefault(x => x.Id == currentId) ?? rankings.First();
        return true;
    }

    public async Task Create(CustomRankingModel model)
    {
        var ranking = model.MapToEntity();
        await _rankings.Create(ranking);
        _current ??= ranking;
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
        if (_current?.Id == ranking.Id)
        {
            _current = Rankings.FirstOrDefault();
            EmitChanged();
        }
    }

    public async Task ArchiveEnduranceEvent()
    {
        GuardHelper.ThrowIfDefault(_socketContext.Event);

        var rankings = await _rankings.ReadMany();
        var ranklists = rankings.Select(x => new Ranklist(x)).Where(x => x.Entries.Any());
        var entry = new ArchiveEntry(_socketContext.Event, _loadedOfficials, ranklists);
        await _archive.Create(entry);
    }

    public RanklistDocument Create(Ranking ranking)
    {
        var enduranceEvent = GuardHelper.ThrowIfDefault(_socketContext.Event);
        var ranklist = new Ranklist(ranking);
        return new RanklistDocument(ranklist, enduranceEvent, _loadedOfficials);
    }

    public void Select(Ranking ranking)
    {
        _current = ranking;
        EmitChanged();
    }

    public async Task Handle(PhaseCompleted notification, CancellationToken cancellationToken)
    {
        await _coalesced.Invoke(() => UpdateRanklist(notification));
    }

    public async Task Handle(ParticipationEliminated notification, CancellationToken cancellationToken)
    {
        await _coalesced.Invoke(() => UpdateRanklist(notification));
    }

    public async Task Handle(ParticipationRestored notification, CancellationToken cancellationToken)
    {
        await _coalesced.Invoke(() => UpdateRanklist(notification));
    }

    public async Task Handle(EventConnected notification, CancellationToken cancellationToken)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken cancellationToken)
    {
        Rankings.Clear();
        _current = null;
        _loadedOfficials = [];
        ClearState();
        return Task.CompletedTask;
    }

    async Task UpdateRanklist(ParticipationPayload payload)
    {
        await Load();

        if (!Rankings.Any())
        {
            return;
        }

        var currentId = _current?.Id;
        var isUpdated = false;
        foreach (var ranking in Rankings)
        {
            if (!ranking.Update(payload.Participation))
            {
                continue;
            }
            await _rankings.Update(ranking);
            isUpdated = true;
        }

        if (!isUpdated)
        {
            return;
        }

        _current = Rankings.FirstOrDefault(x => x.Id == currentId) ?? Rankings.FirstOrDefault();
        EmitChanged();
    }
}
