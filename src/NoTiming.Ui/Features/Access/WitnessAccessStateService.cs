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

    public WitnessAccessContext(INtsSocketContext socketContext, IAccountSession account, JsonApiClient api)
        : base(account)
    {
        _socketContext = socketContext;
        _api = api;
    }

    public WitnessAccessLevel AccessLevel { get; private set; }

    protected override async Task<bool> InitializeState()
    {
        await Account.Load();
        if (!Account.IsKnown)
        {
            return false; // the host has not said who is signed in: nobody is not an answer, so ask again
        }

        if (!Account.IsSignedIn)
        {
            AccessLevel = WitnessAccessLevel.Anonymous;
            return true;
        }

        AccessLevel = WitnessAccessLevel.Registered;
        if (_socketContext.Event is not { } selected)
        {
            return true;
        }

        if (await CanSendSnapshots(selected.Id))
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

    /// <summary>
    /// Whether the Api lets the caller send a Snapshot to the Event. What it cannot answer, because the caller is not signed
    /// in to it, is a no: the Snapshot is refused by the Api again when it is sent. An Api that cannot be reached leaves the
    /// state to be loaded again, as it does for every stateful service.
    /// </summary>
    async Task<bool> CanSendSnapshots(Guid eventId)
    {
        var response = await _api.Send(HttpMethod.Get, $"events/{eventId}/capabilities");
        return response is { IsSuccess: true, Document: { } document }
            && document.GetProperty("data").GetProperty("attributes").TryGetProperty("canSnapshot", out var can)
            && can.ValueKind == JsonValueKind.True;
    }
}
