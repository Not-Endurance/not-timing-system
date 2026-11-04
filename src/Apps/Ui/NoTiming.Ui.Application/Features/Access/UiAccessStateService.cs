using MediatR;
using Not.Application.Authentication.Abstractions;
using Not.Application.Behinds.Adapters;
using Not.Injection;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Presentlists;
using NTS.Application.Contracts.Socket;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects;

namespace NoTiming.Ui.Application.Features.Access;

public class UiAccessContext
    : NStatefulService,
        IUiAccessContext,
        IPresentlistAccess,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly INtsSocketContext _socketContext;
    readonly INUserSession _userSessionService;
    readonly IEventScopedRepository<Official> _officialReader;
    readonly IEventScopedRepository<Operator> _operatorReader;

    public UiAccessContext(
        INtsSocketContext socketContext,
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

        var session = await _userSessionService.GetCurrent<NtsUserSessionStateModel>();
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
