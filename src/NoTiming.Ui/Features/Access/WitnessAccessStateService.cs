using System.Text.Json;
using MediatR;
using Not.Application.HTTP;
using Not.Injection;
using NoTiming.Ui.Features.Account;
using NTS.Contracts.Features.Access;
using NTS.Contracts.Features.Account;
using NTS.Contracts.Socket;
using NTS.Domain.Core.Events;

namespace NoTiming.Ui.Features.Access;

/// <summary>
/// What the person at this Witness may do about the Event it follows. Who they are is told by the host (the account
/// session), and whether they may send a Snapshot is told by the Api, which is the one that decides it (ADR-0012): the
/// capabilities of the Event for the signed-in caller, and not the lists of its Officials and Operators, which the Api does
/// not give away and a client could only guess a decision from.
/// </summary>
public class WitnessAccessContext
    : AccountAwareContext,
        IWitnessAccessContext,
        INotificationHandler<EventConnected>,
        INotificationHandler<EventDisconnected>,
        IScoped
{
    readonly INtsSocketContext _socketContext;
    readonly JsonApiClient _api;
    (Guid Person, Guid? Event, bool CanSend)? _said;

    public WitnessAccessContext(INtsSocketContext socketContext, IAccountSession account, JsonApiClient api)
        : base(account)
    {
        _socketContext = socketContext;
        _api = api;
    }

    public bool IsKnown { get; private set; }

    public bool IsSignedIn { get; private set; }

    public bool CanSendSnapshots { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        await Account.Load();
        if (!Account.IsKnown)
        {
            return false; // the host has not said who is signed in: nobody is not an answer, so ask again
        }

        if (Account.Current is not { } person)
        {
            IsKnown = true;
            IsSignedIn = false;
            CanSendSnapshots = false;
            return true;
        }

        // What may be done is said once, when the Api has answered: a person who may send Snapshots is not shown as one who may
        // not for the time it takes to ask again, which a page that decides by it (the Snapshot page) would act on. An Api that
        // cannot answer leaves what it last said of this person and Event, so that a phone that lost its connection for a moment
        // does not take the page and the sending from an Official (the Api refuses what they may no longer do when it is sent),
        // and the person may not when it has said nothing; and the state is loaded again.
        var eventId = _socketContext.Event?.Id;
        var canSend = _said is { } said && said.Person == person.Id && said.Event == eventId && said.CanSend;
        try
        {
            if (eventId is { } id)
            {
                canSend = await MaySendTo(id);
            }

            _said = (person.Id, eventId, canSend);
        }
        finally
        {
            IsKnown = true;
            IsSignedIn = true;
            CanSendSnapshots = canSend;
        }

        // With no Event followed there is nothing to ask the Api, and what is said then is not what the Event will say. The state
        // is not taken for loaded, so that whoever loads it once there is an Event (the viewed Event, which is opened as soon as
        // the connection names the Event, while the connection is still handing it to the services that follow it) is told what
        // the Api says of the person and that Event, and not the level of a person who followed none.
        return eventId != null;
    }

    public async Task Handle(EventConnected notification, CancellationToken ct)
    {
        await ReloadState();
    }

    public async Task Handle(EventDisconnected notification, CancellationToken ct)
    {
        await ReloadState();
    }

    /// <summary>
    /// Whether the Api lets the caller send a Snapshot to the Event. What it cannot answer, because the caller is not signed
    /// in to it, is a no: the Snapshot is refused by the Api again when it is sent. An Api that cannot be reached, or that
    /// fails (5xx), has not answered: the state is loaded again, as it is for every stateful service.
    /// </summary>
    async Task<bool> MaySendTo(Guid eventId)
    {
        var response = await _api.Send(HttpMethod.Get, $"events/{eventId}/capabilities");
        if ((int)response.Status >= 500)
        {
            throw response.ToException();
        }

        return response is { IsSuccess: true, Document: { } document }
            && document.GetProperty("data").GetProperty("attributes").TryGetProperty("canSnapshot", out var can)
            && can.ValueKind == JsonValueKind.True;
    }
}
