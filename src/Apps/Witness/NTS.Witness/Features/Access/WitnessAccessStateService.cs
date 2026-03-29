using MediatR;
using Not.Application.Authentication.Abstractions;
using Not.Application.Behinds.Adapters;
using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Application.Socket;
using NTS.Application.Watcher;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Events;

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
    readonly IReadMany<Official> _officialReader;

    public WitnessAccessContext(
        INtsSocketContext socketContext,
        INUserSession userSessionService,
        IReadMany<Official> officialReader
    )
    {
        _socketContext = socketContext;
        _userSessionService = userSessionService;
        _officialReader = officialReader;
    }

    public WitnessAccessLevel AccessLevel { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        AccessLevel = WitnessAccessLevel.Unknown;
        if (_socketContext.Event == null)
        {
            return true;
        }

        var session = await _userSessionService.GetCurrent<NtsUserSessionModel>();
        var userId = session?.User.Id;
        if (userId == null)
        {
            return true;
        }

        var officials = await _officialReader.ReadMany();
        AccessLevel = officials.Any(x => x.UserId == userId.Value)
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
}
