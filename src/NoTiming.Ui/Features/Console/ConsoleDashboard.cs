using Not.Application.Behinds.Adapters;
using Not.Domain.Exceptions;
using Not.Injection;
using Not.Krud.Abstractions;
using NoTiming.Ui.Features.Core;
using NoTiming.Ui.Features.Core.Dashboard;
using NTS.Contracts.Core;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Features.Console;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects.Snapshots;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NoTiming.Ui.Features.Console;

/// <summary>
/// The dashboard of a Live Event in the Console (#646). The Participations are a view over the store (ADR-0006), and what is
/// kept here is the selection of the Console Operator and the numbers it recorded a time for. A change is made on the
/// Participation of the store and written to the Api against the version the Participation was read at (ADR-0013): when the Api
/// refuses it, because the Participation changed meanwhile or for another reason, the Participation is read again, so that
/// what is shown is what is stored and not the change that was not taken, and what the Api said is thrown, so that the form
/// that sent the change keeps what was typed.
/// </summary>
public class ConsoleDashboard : NStatefulService, IConsoleDashboard, IKrudFormService<PhaseUpdateModel>, IScoped
{
    const string PARTICIPATION_CHANGED = "participation-changed";

    readonly IParticipationStore _store;
    readonly ConsoleParticipationWrites _writes;
    readonly IServerClock _clock;
    readonly IAccountSession _account;
    readonly ISnapshotPublisher _publisher;
    readonly List<int> _recent = [];
    IReadOnlyList<Participation> _participations = [];
    IViewedEvent? _view;
    Guid? _selected;

    public ConsoleDashboard(
        IParticipationStore store,
        ConsoleParticipationWrites writes,
        IServerClock clock,
        IAccountSession account,
        ISnapshotPublisher publisher
    )
    {
        _store = store;
        _writes = writes;
        _clock = clock;
        _account = account;
        _publisher = publisher;
        Observe(store, Changed);
    }

    public IReadOnlyList<Participation> Participations => _participations;

    public IReadOnlyList<int> RecentlyTimed => _recent;

    public Participation? Selected
    {
        get => _selected is { } id ? _store.Find(id) : null;
        set
        {
            _selected = value?.Id;
            if (value != null)
            {
                _recent.Remove(value.Combination.Number);
            }

            EmitChanged();
        }
    }

    public bool IsEliminated => Selected?.IsEliminated() ?? false;

    public bool IsRepresentRequested => Selected?.PhaseAt(_clock.Now).IsRepresentRequested ?? false;

    public bool IsInspectionRequested => Selected?.PhaseAt(_clock.Now).IsRequiredInspectionRequested ?? false;

    protected override async Task<bool> InitializeState()
    {
        if (_view == null)
        {
            return false;
        }

        await _store.Load();
        await _clock.Synchronise();
        Rebuild();
        return true;
    }

    public async Task Show(IViewedEvent view)
    {
        _view = view;
        await ReloadState();
    }

    public async Task RecordTime(SnapshotType type, TimeSpan timeOfDay)
    {
        var view = Shown();
        view.EnsureCanWrite();
        var participation = Chosen();
        var today = _clock.Now.ToLocalTime();
        var time = new DateTimeOffset(today.Date + timeOfDay, today.Offset);
        var snapshot = new Snapshot(
            participation.Combination.Number,
            participation.Combination.Athlete.Name,
            participation.Combination.Athlete.NameEnglish,
            new Timestamp(time),
            participation.Competition.Ruleset
        );
        var receipts = await _publisher.PublishSnapshotsAsync(view.Event.Id, new SnapshotGroup([snapshot], type));
        if (receipts.FirstOrDefault() is not { IsRecorded: true } recorded)
        {
            view.Refused(receipts.FirstOrDefault()?.ErrorCode);
            throw new DomainException(
                receipts.FirstOrDefault()?.ErrorMessage
                    ?? receipts.FirstOrDefault()?.ErrorCode
                    ?? Time_not_recorded_string
            );
        }

        await _store.Refresh(participation.Id);
        _recent.Add(participation.Combination.Number);
        EmitChanged();
        if (!recorded.IsAccepted)
        {
            throw new DomainException(
                string.Format(Time_not_taken_string, OutcomeWords.Of(recorded.Snapshot!.Outcome.ToString()))
            );
        }
    }

    public Task RequestRepresentation(bool isRequested)
    {
        return Change((participation, now) => participation.ToggleRepresentation(isRequested, now));
    }

    public Task RequestInspection(bool isRequested)
    {
        return Change((participation, now) => participation.ToggleInspection(isRequested, now));
    }

    public Task Withdraw()
    {
        return Change((participation, _) => participation.Withdraw());
    }

    public Task Retire()
    {
        return Change((participation, _) => participation.Retire());
    }

    public Task FinishNotRanked(string reason)
    {
        return Change((participation, _) => participation.FinishNotRanked(reason));
    }

    public Task Disqualify(DisqualifyCode[] codes, string? reason)
    {
        return Change((participation, _) => participation.Disqualify(codes, reason));
    }

    public Task FailToQualify(FailToQualifyCode[] codes, string? reason)
    {
        return Change((participation, _) => participation.FailToQualify(codes, reason));
    }

    public Task RestoreQualification()
    {
        return Change((participation, _) => participation.Restore());
    }

    public async Task ChangeTimeEvents(IReadOnlyCollection<Guid> disable, IReadOnlyCollection<Guid> enable)
    {
        Shown().EnsureCanWrite();
        try
        {
            await Write(Chosen(), (participation, now) => participation.ChangeTimeEvents(disable, enable, now));
        }
        catch (TimelineBlockedException blocked)
        {
            throw new DomainException(
                string.Format(Time_events_blocked_string, TimelineWords.Describe(Chosen(), blocked.Blocking))
            );
        }
    }

    public Task Create(PhaseUpdateModel item)
    {
        throw new NotSupportedException("A Phase is made by the Setup and never by the dashboard.");
    }

    /// <summary>
    /// The times of a Phase as the person typed them: the ones that differ from the ones shown are one edit of the Phase, and a
    /// Save that changes nothing writes nothing.
    /// </summary>
    public async Task Update(PhaseUpdateModel model)
    {
        Shown().EnsureCanWrite();
        var participation =
            _store.Participations.FirstOrDefault(x => x.Phases.Any(y => y.Id == model.Id))
            ?? throw new DomainException(Participation_not_found_string);
        await _account.Load();
        var actor = _account.Current?.Id ?? throw new DomainException(Not_signed_in_string);
        try
        {
            await Write(participation, (changed, now) => changed.Update(model, actor, now));
        }
        catch (DomainPropertyException invalid) when (invalid.Property != null)
        {
            // what is said of a time is shown at the input that holds it, where the person who corrects it clears it
            throw new DomainPropertyException(PhaseUpdateModel.InputOf(invalid.Property), invalid.Message);
        }
    }

    /// <summary>What the store holds changed: the list is rebuilt, and a selection that is gone is let go.</summary>
    void Changed()
    {
        Rebuild();
        EmitChanged();
    }

    void Rebuild()
    {
        _participations = [.. _store.Participations.OrderBy(x => x.Combination.Number)];
        if (_selected is { } id && _store.Find(id) == null)
        {
            _selected = null;
        }

        _selected ??= _participations.FirstOrDefault()?.Id;
    }

    Task Change(Action<Participation, DateTimeOffset> change)
    {
        Shown().EnsureCanWrite();
        return Write(Chosen(), change);
    }

    Task Write(Participation participation, Action<Participation, DateTimeOffset> change)
    {
        return Write(
            participation,
            (changed, now) =>
            {
                change(changed, now);
                return true;
            }
        );
    }

    /// <summary>Applies the change to the Participation of the store and writes it, unless the change says it changed nothing.</summary>
    async Task Write(Participation participation, Func<Participation, DateTimeOffset, bool> change)
    {
        var id = participation.Id;
        try
        {
            if (!change(participation, _clock.Now))
            {
                return;
            }
        }
        catch
        {
            await _store.Refresh(id); // what was half done is not what is stored
            throw;
        }

        await _writes.Update(participation);
        var refused = _writes.LastError;
        await _store.Refresh(id); // the version that was written, or what is stored when the Api would not
        EmitChanged();
        if (refused != null)
        {
            Shown().Refused(refused.Code);
            throw new DomainException(
                refused.Code == PARTICIPATION_CHANGED ? Participation_changed_string : refused.Message
            );
        }
    }

    IViewedEvent Shown()
    {
        return _view ?? throw new InvalidOperationException("The dashboard shows no Event.");
    }

    Participation Chosen()
    {
        return Selected ?? throw new DomainException(Select_a_participation_string);
    }
}
