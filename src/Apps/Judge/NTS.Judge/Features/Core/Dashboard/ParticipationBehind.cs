using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Application.Services;
using Not.Async.Extensions;
using Not.Exceptions;
using Not.Injection;
using Not.Krud.Abstractions;
using Not.Safe;
using Not.Startup;
using NTS.Application.Core;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Judge.Features.Core.State;

namespace NTS.Judge.Features.Core.Dashboard;

public class ParticipationBehind
    : NStatefulService,
        IKrudFormService<PhaseUpdateModel>,
        IInspectionService,
        IEliminationService,
        IParticipationContext,
        IUpdateBehind<PhaseUpdateModel>,
        ITimingService,
        IStartupInitializerAsync,
        ICoreDependentObservables,
        ISingleton
{
    readonly List<int> _recentlyProcessed = [];
    readonly IRepository<Participation> _participationRepository;
    readonly IRepository<SnapshotResult> _snapshotResultRepository;
    Participation? _selectedParticipation;

    public ParticipationBehind(
        IRepository<Participation> participationRepository,
        IRepository<SnapshotResult> snapshotResultRepository
    )
    {
        _participationRepository = participationRepository;
        _snapshotResultRepository = snapshotResultRepository;
    }

    public IReadOnlyList<int> RecentlyTimed => _recentlyProcessed;
    public IReadOnlyList<Participation> Participations { get; private set; } = [];
    public bool IsInspectionRequested => Selected?.Phases.Current.IsRequiredInspectionRequested ?? false;
    public bool IsRepresentRequested => Selected?.Phases.Current.IsReinspectionRequested ?? false;
    public bool IsEliminated => Selected?.Eliminated != null;
    public Participation? Selected
    {
        get => _selectedParticipation;
        set
        {
            _selectedParticipation = value;
            var number = _selectedParticipation?.Combination.Number;
            if (number != null)
            {
                _recentlyProcessed.Remove(number.Value);
            }
            EmitChanged();
        }
    }

    protected override async Task<bool> InitializeState()
    {
        Participations = await _participationRepository.ReadMany().AsReadOnly();
        Selected = Participations.FirstOrDefault();
        return Participations.Any();
    }

    public async Task RunAtStartupAsync()
    {
        await InitializeState();
    }

    public Task Create(PhaseUpdateModel item)
    {
        throw new NotImplementedException();
    }

    public async Task Update(PhaseUpdateModel model)
    {
        var participation = Participations.FirstOrDefault(x => x.Phases.Any(y => y.Id == model.Id));
        GuardHelper.ThrowIfDefault(participation);

        participation.Update(model);
        await _participationRepository.Update(participation);
        EmitChanged();
    }

    public async Task Record(Snapshot snapshot)
    {
        var participation = Participations.FirstOrDefault(x => x.Combination.Number == snapshot.Number);
        if (participation == null)
        {
            return;
        }
        var result = participation.Process(snapshot);
        if (result.Type == SnapshotResultType.Applied)
        {
            await _participationRepository.Update(participation);
        }
        await _snapshotResultRepository.Create(result);
        _recentlyProcessed.Add(participation.Combination.Number);
        EmitChanged();
    }

    public async Task RequestRepresent(bool isRequested)
    {
        Selected!.ToggleRepresentation(isRequested);
        await _participationRepository.Update(Selected);
        EmitChanged();
    }

    public async Task RequestInspection(bool isRequested)
    {   
        Selected!.ToggleInspection(isRequested);
        await _participationRepository.Update(Selected);
        EmitChanged();
    }

    public async Task Withdraw()
    {
        await SafeHelper.Run(SafeWithdraw);
    }

    public async Task Retire()
    {
        await SafeHelper.Run(SafeRetire);
    }

    public async Task FinishNotRanked(string reason)
    {
        Task action() => SafeFinishNotRanked(reason);
        await SafeHelper.Run(action);
    }

    public async Task Disqualify(DisqualifyCode[] dqCodes, string? reason)
    {
        Task action() => SafeDisqualify(dqCodes, reason);
        await SafeHelper.Run(action);
    }

    public async Task FailToQualify(FailToQualifyCode[] ftqCodes, string? reason)
    {
        Task action() => SafeFailToQualify(ftqCodes, reason);
        await SafeHelper.Run(action);
    }

    public async Task RestoreQualification()
    {
        await SafeHelper.Run(SafeRestoreQualification);
    }

    async Task SafeWithdraw()
    {
        GuardHelper.ThrowIfDefault(Selected);
        Selected.Withdraw();
        await _participationRepository.Update(Selected);

        EmitChanged();
    }

    async Task SafeRetire()
    {
        GuardHelper.ThrowIfDefault(Selected);
        Selected.Retire();
        await _participationRepository.Update(Selected);

        EmitChanged();
    }

    async Task SafeFinishNotRanked(string reason)
    {
        GuardHelper.ThrowIfDefault(Selected);
        Selected.FinishNotRanked(reason);
        await _participationRepository.Update(Selected);

        EmitChanged();
    }

    async Task SafeDisqualify(DisqualifyCode[] dqCodes, string? reason)
    {
        GuardHelper.ThrowIfDefault(Selected);
        Selected.Disqualify(dqCodes, reason);
        await _participationRepository.Update(Selected);

        EmitChanged();
    }

    async Task SafeFailToQualify(FailToQualifyCode[] ftqCodes, string? reason)
    {
        GuardHelper.ThrowIfDefault(Selected);

        Selected.FailToQualify(ftqCodes, reason);
        await _participationRepository.Update(Selected);

        EmitChanged();
    }

    async Task SafeRestoreQualification()
    {
        GuardHelper.ThrowIfDefault(Selected);
        Selected.Restore();
        await _participationRepository.Update(Selected);

        EmitChanged();
    }
}
