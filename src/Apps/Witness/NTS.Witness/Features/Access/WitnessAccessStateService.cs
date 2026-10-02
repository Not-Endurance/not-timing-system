using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Not.Application.Authentication.Abstractions;
using Not.Injection;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Socket;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;
using NTS.Domain.Core.Objects;
using NTS.Witness.Features.Sessions;

namespace NTS.Witness.Features.Access;

public class WitnessAccessContext
    : WitnessAuthenticationAwareContext,
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
        IEventScopedRepository<Operator> operatorRepository,
        AuthenticationStateProvider authenticationStateProvider
    )
        : base(authenticationStateProvider)
    {
        _socketContext = socketContext;
        _userSessionService = userSessionService;
        _officialReader = officialRepository;
        _operatorReader = operatorRepository;
    }

    public WitnessAccessLevel AccessLevel { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        var session = await _userSessionService.GetCurrent<NtsUserSessionStateModel>();
        var userId = session?.User.Id;
        if (userId == null)
        {
            AccessLevel = WitnessAccessLevel.Anonymous;
            return true;
        }

        AccessLevel = WitnessAccessLevel.Registered;
        if (_socketContext.Event == null)
        {
            return true;
        }

        var officials = await _officialReader.ReadMany();
        var operators = await _operatorReader.ReadMany();
        if (CanWriteSnapshots(userId.Value, officials, operators))
        {
            AccessLevel = WitnessAccessLevel.Official;
        }

        return true;
    }

    public async Task Handle(EventConnected notification, CancellationToken ct)
    {
        await ReloadState();
    }

    public async Task Handle(EventDisconnected notification, CancellationToken ct)
    {
        await ReloadState();
    }

    static bool CanWriteSnapshots(int userId, IEnumerable<Official> officials, IEnumerable<Operator> operators)
    {
        return operators.Any(x => x.UserId == userId && SnapshotAccessPolicy.CanWriteAsOperator(x.Role))
            || officials.Any(x => x.UserId == userId && SnapshotAccessPolicy.CanWriteAsOfficial(x.Role));
    }
}
