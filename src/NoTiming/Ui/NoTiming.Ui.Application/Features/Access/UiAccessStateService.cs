using MediatR;
using NC.Application.Authentication.Abstractions;
using NC.Application.Behinds.Adapters;
using NC.Injection;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Presentlists;
using NoTiming.Application.Contracts.Socket;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Domain.Core.Aggregates;
using NoTiming.Domain.Core.Events;
using NoTiming.Domain.Core.Objects;

namespace NoTiming.Ui.Application.Features.Access;

public class UiAccessContext
    : NStatefulService,
        IUiAccessContext,
        IPresentlistAccess,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly INoTimingSocketContext _socketContext;
    readonly INUserSession _userSessionService;
    readonly IEventScopedRepository<Official> _officialReader;
    readonly IEventScopedRepository<Operator> _operatorReader;

    public UiAccessContext(
        INoTimingSocketContext socketContext,
        INUserSession userSessionService,
        IEventScopedRepository<Official> officialRepository,
        IEventScopedRepository<Operator> operatorRepository
    )
    {
        _socketContext = socketContext;
        _userSessionService = userSessionService;
        _officialReader = officialRepository;
        _operatorReader = operatorRepository;
    }

    public UiAccessLevel AccessLevel { get; private set; }
    public bool CanAcknowledgePresentations => AccessLevel == UiAccessLevel.Official;

    protected override async Task<bool> InitializeState()
    {
        AccessLevel = UiAccessLevel.Unknown;
        if (_socketContext.Event == null)
        {
            return true;
        }

        var session = await _userSessionService.GetCurrent<NoTimingUserSessionStateModel>();
        var userId = session?.User.Id;
        if (userId == null)
        {
            return true;
        }

        var officials = await _officialReader.ReadMany();
        var operators = await _operatorReader.ReadMany();
        AccessLevel = CanWriteSnapshots(userId.Value, officials, operators)
            ? UiAccessLevel.Official
            : UiAccessLevel.Participant;

        return true;
    }

    public async Task Handle(EventConnected notification, CancellationToken ct)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken ct)
    {
        AccessLevel = UiAccessLevel.Unknown;
        ClearState();
        return Task.CompletedTask;
    }

    static bool CanWriteSnapshots(int userId, IEnumerable<Official> officials, IEnumerable<Operator> operators)
    {
        return operators.Any(x => x.UserId == userId && SnapshotAccessPolicy.CanWriteAsOperator(x.Role))
            || officials.Any(x => x.UserId == userId && SnapshotAccessPolicy.CanWriteAsOfficial(x.Role));
    }
}
