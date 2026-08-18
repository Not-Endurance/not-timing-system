using MediatR;
using Not.Application.Authentication.Abstractions;
using Not.Application.Behinds.Adapters;
using Not.Injection;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects;

namespace NTS.Witness.Features.Access;

public class WitnessAccessContext
    : NStatefulService,
        IWitnessAccessContext,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly INtsSocketContext _socketContext;
    readonly INUserSession _userSessionService;
    readonly IEventScopedRepository<Official> _officialReader;
    readonly IEventScopedRepository<Operator> _operatorReader;

    public WitnessAccessContext(
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

    public WitnessAccessLevel AccessLevel { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        AccessLevel = WitnessAccessLevel.Unknown;
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
            ? WitnessAccessLevel.Official
            : WitnessAccessLevel.Participant;

        return true;
    }

    public async Task Handle(EventConnected notification, CancellationToken ct)
    {
        await ReloadState();
    }

    public Task Handle(EventDisconnected notification, CancellationToken ct)
    {
        AccessLevel = WitnessAccessLevel.Unknown;
        ClearState();
        return Task.CompletedTask;
    }

    static bool CanWriteSnapshots(int userId, IEnumerable<Official> officials, IEnumerable<Operator> operators)
    {
        return operators.Any(x => x.UserId == userId && SnapshotAccessPolicy.CanWriteAsOperator(x.Role))
            || officials.Any(x => x.UserId == userId && SnapshotAccessPolicy.CanWriteAsOfficial(x.Role));
    }
}
