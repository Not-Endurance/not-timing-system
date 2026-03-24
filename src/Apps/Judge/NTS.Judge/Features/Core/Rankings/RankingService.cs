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
    readonly INtsSocketContext _socketContext;
    readonly IRepository<Ranking> _rankings;
    readonly IRepository<Official> _officials;
    readonly IRepository<ArchiveEntry> _archive;
    readonly CoalesceInvoker _coalesced;
    Ranking? _current;

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
            return false;
        }
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

        var officials = await _officials.ReadMany();
        var rankings = await _rankings.ReadMany();
        var ranklists = rankings.Select(x => new Ranklist(x)).Where(x => x.Entries.Any());
        var entry = new ArchiveEntry(_socketContext.Event, officials, ranklists);
        await _archive.Create(entry);
    }

    public async Task<RanklistDocument> Create(Ranking ranking)
    {
        var enduranceEvent = GuardHelper.ThrowIfDefault(_socketContext.Event);
        var officials = await _officials.ReadMany();
        var ranklist = new Ranklist(ranking);
        return new RanklistDocument(ranklist, enduranceEvent, officials);
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
            //TODO: Implement UpdateMany
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
