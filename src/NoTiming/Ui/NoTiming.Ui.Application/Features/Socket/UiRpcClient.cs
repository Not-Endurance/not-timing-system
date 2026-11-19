using NC.Application.DomainEvents;
using NC.Application.Identity;
using NC.Application.RPC;
using NC.Application.RPC.Clients;
using NC.Injection;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Notices;
using NoTiming.Application.Contracts.Presentlists;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Events;
using NoTiming.Domain.Core.Objects.Presentlists;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Objects;
using NoTiming.Domain.Watcher;
using NoTiming.Ui.Application.Features.Core.Dashboard;

namespace NoTiming.Ui.Application.Features.Socket;

/// <summary>Receives participation change notices and presents the fetched state to the Ui's handlers.</summary>
public class UiRpcClient : RpcClient, IPresentlistActionPublisher, IScoped
{
    readonly IDomainEventDispatcher _domainEventDispatcher;
    readonly IEventScopedRepository<Participation> _participations;
    readonly ISnapshotPublisher _snapshotPublisher;

    public UiRpcClient(
        IRpcSocket socket,
        IClientIdentity identity,
        IEventScopedRepository<Participation> participations,
        IDomainEventDispatcher domainEventDispatcher,
        ISnapshotPublisher snapshotPublisher
    )
        : base(socket, identity)
    {
        _participations = participations;
        _domainEventDispatcher = domainEventDispatcher;
        _snapshotPublisher = snapshotPublisher;
    }

    protected override void RegisterProcedures()
    {
        RegisterChangeProcedure<ParticipationChangedNotice>("ParticipationChanged", OnParticipationChanged);
    }

    public async Task PublishPresentation(PresentlistEntry entry)
    {
        var snapshot = new Snapshot(
            entry.Number,
            entry.AthleteName,
            entry.AthleteNameEnglish,
            Timestamp.Now(),
            entry.EnglishNames
        );
        await _snapshotPublisher.PublishSnapshotsAsync(new SnapshotGroup([snapshot], SnapshotType.Present));
    }

    async Task OnParticipationChanged(ParticipationChangedNotice notice, bool isRemote)
    {
        var participation = await _participations.Read(x => x.Id == notice.ParticipationId);
        if (participation == null)
        {
            return;
        }

        await _domainEventDispatcher.Dispatch(new ParticipationChanged(participation, null, isRemote));
    }
}
