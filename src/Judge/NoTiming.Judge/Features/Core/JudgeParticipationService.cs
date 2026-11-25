using NC.Application.Behinds.Adapters;
using NC.Application.DomainEvents;
using NC.Async.Extensions;
using NC.Exceptions;
using NC.Injection;
using NC.Krud.Abstractions;
using NC.Startup;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Aggregates.Participations;
using NoTiming.Domain.Core.Events;
using NoTiming.Judge.Contracts.Features.Core.Dashboard;
using NoTiming.Judge.Features.Core.State;

namespace NoTiming.Judge.Features.Core;

/// <summary>
/// Holds the judge's view of the participations. Changes are made by the API, which returns the new state. Changes made
/// by other clients arrive as notices and are fetched here.
/// </summary>
public class JudgeParticipationService
    : NStatefulService,
        IKrudFormService<PhaseUpdateModel>,
        IInspectionService,
        IEliminationService,
        IParticipationContext,
        IJudgeSnapshotService,
        IStartupInitializerAsync,
        ICoreDependentObservables,
        IScoped
{
    readonly List<int> _recentlyChanged = [];
    readonly SemaphoreSlim _refreshGate = new(1, 1);
    readonly IParticipationCommands _commands;
    readonly IDomainEventDispatcher _domainEventDispatcher;
    readonly IEventScopedRepository<Participation> _participationRepository;
    readonly INoTimingSocketContext _socketContext;
    Participation? _selectedParticipation;

    public JudgeParticipationService(
        INoTimingSocketContext socketContext,
        IEventScopedRepository<Participation> participationRepository,
        IParticipationCommands commands,
        IDomainEventDispatcher domainEventDispatcher
    )
    {
        _socketContext = socketContext;
        _participationRepository = participationRepository;
        _commands = commands;
        _domainEventDispatcher = domainEventDispatcher;
    }

    public IReadOnlyList<int> RecentlyChanged => _recentlyChanged;
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
                _recentlyChanged.Remove(number.Value);
            }
            EmitChanged();
        }
    }

    protected override async Task<bool> InitializeState()
    {
        if (!_socketContext.IsConnected)
        {
            return false;
        }
        var selected = _selectedParticipation;
        Participations = await _participationRepository.ReadMany().AsReadOnly();
        Selected = ResolveSelectedParticipation(selected);
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
        var updated = await _commands.UpdatePhase(model);
        await ApplyOwnChange(updated);
    }

    /// <summary>A snapshot this judge recorded. The API applies it and returns the participations it changed.</summary>
    public async Task Record(Snapshot snapshot)
    {
        var entry = new SnapshotModel
        {
            Number = snapshot.Number,
            Name = string.Empty,
            Timestamp = snapshot.Timestamp.ToString(),
        };
        var group = new SnapshotGroupModel { Entries = [entry], Type = snapshot.Type };
        var changed = await _commands.RecordSnapshots(group);
        foreach (var participation in changed)
        {
            await ApplyChange(participation, isRemote: false);
        }
        EmitChanged();
    }

    /// <summary>
    /// A change made by any client, announced by the event bus. The state is fetched again, and refreshes run one at a
    /// time, so a slower read cannot put older state in place of newer state.
    /// </summary>
    public async Task Refresh(int participationId, bool isRemote)
    {
        await _refreshGate.WaitAsync();
        try
        {
            var participation = await _participationRepository.Read(x => x.Id == participationId);
            if (participation == null)
            {
                return;
            }

            await ApplyChange(participation, isRemote);
            EmitChanged();
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async Task RequestRepresent(bool isRequested)
    {
        var updated = await _commands.SetRepresentation(GuardSelected().Id, isRequested);
        await ApplyOwnChange(updated);
    }

    public async Task RequestInspection(bool isRequested)
    {
        var updated = await _commands.SetInspection(GuardSelected().Id, isRequested);
        await ApplyOwnChange(updated);
    }

    public async Task Withdraw()
    {
        var updated = await _commands.Withdraw(GuardSelected().Id);
        await ApplyOwnChange(updated);
    }

    public async Task Retire()
    {
        var updated = await _commands.Retire(GuardSelected().Id);
        await ApplyOwnChange(updated);
    }

    public async Task FinishNotRanked(string reason)
    {
        var updated = await _commands.FinishNotRanked(GuardSelected().Id, reason);
        await ApplyOwnChange(updated);
    }

    public async Task Disqualify(DisqualifyCode[] dqCodes, string? reason)
    {
        var updated = await _commands.Disqualify(GuardSelected().Id, dqCodes, reason);
        await ApplyOwnChange(updated);
    }

    public async Task FailToQualify(FailToQualifyCode[] ftqCodes, string? reason)
    {
        var updated = await _commands.FailToQualify(GuardSelected().Id, ftqCodes, reason);
        await ApplyOwnChange(updated);
    }

    public async Task RestoreQualification()
    {
        var updated = await _commands.RestoreQualification(GuardSelected().Id);
        await ApplyOwnChange(updated);
    }

    Participation GuardSelected()
    {
        GuardHelper.ThrowIfDefault(Selected);
        return Selected!;
    }

    /// <summary>The state this judge's own command returned. It is applied at once, without waiting for the notice.</summary>
    async Task ApplyOwnChange(Participation? updated)
    {
        if (updated != null)
        {
            await ApplyChange(updated, isRemote: false);
        }
        EmitChanged();
    }

    async Task ApplyChange(Participation participation, bool isRemote)
    {
        var list = Participations.ToList();
        var index = list.FindIndex(x => x.Id == participation.Id);
        Participation? previous = null;
        if (index >= 0)
        {
            previous = list[index];
            list[index] = participation;
        }
        else
        {
            list.Add(participation);
        }
        Participations = list.AsReadOnly();

        if (Selected?.Id == participation.Id)
        {
            _selectedParticipation = participation;
        }

        var number = participation.Combination.Number;
        if (isRemote && !_recentlyChanged.Contains(number))
        {
            _recentlyChanged.Add(number);
        }

        await _domainEventDispatcher.Dispatch(new ParticipationChanged(participation, previous, isRemote));
    }

    Participation? ResolveSelectedParticipation(Participation? selected)
    {
        if (!Participations.Any())
        {
            return null;
        }
        if (selected == null)
        {
            return Participations.FirstOrDefault();
        }

        return Participations.FirstOrDefault(x => x.Id == selected.Id)
            ?? Participations.FirstOrDefault(x => x.Combination.Number == selected.Combination.Number)
            ?? Participations.FirstOrDefault();
    }
}
